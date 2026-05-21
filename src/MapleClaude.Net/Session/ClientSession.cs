using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using MapleClaude.Domain;
using MapleClaude.Net.Crypto;
using MapleClaude.Net.Packet;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net.Session;

/// <summary>
/// Owns one socket to a MapleStory server (login or channel). Reads the
/// unencrypted handshake, then drives the framed cipher pipeline:
/// <see cref="PacketCipher"/> for headers + Shanda + Maple AES + InnoHash.
/// All socket I/O lives on background tasks; decoded packets are queued
/// and drained on the main thread via <see cref="DrainInbound"/>.
/// </summary>
public sealed class ClientSession : IDisposable
{
    private readonly ILogger<ClientSession> _logger;
    private readonly PacketRouter _router;
    private readonly ConcurrentQueue<byte[]> _inbound = new();
    private readonly ConcurrentQueue<Action> _events = new();
    private CancellationTokenSource? _cts;
    private Socket? _socket;
    private NetworkStream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;
    private readonly byte[] _sendIv = new byte[4];
    private readonly byte[] _recvIv = new byte[4];
    private readonly byte[] _sendIvLock = new byte[1];
    private readonly byte[] _recvIvLock = new byte[1];
    private bool _handshakeComplete;

    /// <summary>16-byte machine fingerprint shipped in CheckPassword + MigrateIn.</summary>
    public byte[] MachineId { get; set; } = new byte[16];

    /// <summary>8-byte handshake key returned by CheckPasswordResult.</summary>
    public Account Account { get; set; } = new();

    /// <summary>List of worlds accumulated from WorldInformation packets.</summary>
    public List<WorldInfo> Worlds { get; } = new();

    /// <summary>Character list from the last SelectWorldResult.</summary>
    public List<CharacterEntry> Characters { get; } = new();

    /// <summary>Last decoded server-hello (so MigrationCoordinator can re-use it).</summary>
    public HandshakeInfo? Handshake { get; private set; }

    /// <summary>Fired (on the network thread) when the handshake completes.</summary>
    public event Action<HandshakeInfo>? HandshakeReceived;

    /// <summary>Fired (on the network thread) on graceful or error disconnect.</summary>
    public event Action<Exception?>? Disconnected;

    public bool IsConnected => _socket?.Connected == true;

    /// <summary>The packet router this session dispatches to. Exposed so stage-owned
    /// handlers can register opcodes on the same router the session reads from.</summary>
    public PacketRouter PacketRouter => _router;

    /// <summary>Convenience: subscribe a body-only callback (no session arg) for one S→C opcode.</summary>
    public void RegisterHandler(OutHeader header, Action<InPacket> callback)
        => _router.Register(header, (p, _) => callback(p));

    /// <summary>Remove the handler previously registered via <see cref="RegisterHandler(OutHeader, Action{InPacket})"/>.</summary>
    public void UnregisterHandler(OutHeader header)
        => _router.Unregister(header);

    public ClientSession(ILogger<ClientSession> logger, PacketRouter router)
    {
        _logger = logger;
        _router = router;
    }

    /// <summary>Open a fresh TCP socket and start the read loop. Throws on connect failure.</summary>
    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        DisposeSocket();
        _cts = new CancellationTokenSource();
        var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);

        _logger.LogInformation("Connecting to {Host}:{Port}", host, port);
        var addresses = await Dns.GetHostAddressesAsync(host, linked.Token);
        IPAddress addr = Array.Find(addresses, a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        _socket = new Socket(addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };
        await _socket.ConnectAsync(addr, port, linked.Token);
        _stream = new NetworkStream(_socket, ownsSocket: false);
        _reader = PipeReader.Create(_stream);
        _writer = PipeWriter.Create(_stream);
        _handshakeComplete = false;
        Worlds.Clear();
        Characters.Clear();
        _logger.LogInformation("Socket connected to {Endpoint}", _socket.RemoteEndPoint);

        _ = Task.Run(() => ReadLoopAsync(_cts.Token), CancellationToken.None);
    }

    /// <summary>Tear down the socket without raising <see cref="Disconnected"/> as an error.</summary>
    public Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnect requested");
        DisposeSocket();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Back-compat alias for <see cref="DrainInbound"/>. Some stages were written
    /// against an earlier naming and call <c>DrainQueue()</c>; both flush the
    /// same inbound event/packet queue.
    /// </summary>
    public void DrainQueue() => DrainInbound();

    /// <summary>
    /// Drains queued events (HandshakeReceived, Disconnected) and packets onto
    /// the calling thread. Stages call this once per <c>Update</c> tick so all
    /// handler code runs on the MonoGame thread.
    /// </summary>
    public void DrainInbound()
    {
        while (_events.TryDequeue(out var evt))
        {
            try { evt(); } catch (Exception ex) { _logger.LogError(ex, "Drained event threw"); }
        }
        while (_inbound.TryDequeue(out var body))
        {
            try
            {
                _router.Dispatch(new InPacket(body), this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Packet dispatch threw");
            }
        }
    }

    /// <summary>Encrypt + send one already-built <see cref="OutPacket"/>. The packet is released after send.</summary>
    public void Send(OutPacket packet)
    {
        if (!_handshakeComplete || _writer is null)
        {
            _logger.LogWarning("Send called before handshake complete (opcode={Op}); dropping", packet.Header);
            packet.Release();
            return;
        }
        var body = packet.ToArray();
        packet.Release();
        SendRaw(body);
    }

    /// <summary>Encrypt + send one raw body byte array.</summary>
    public void SendRaw(byte[] body)
    {
        if (_writer is null)
        {
            return;
        }
        Span<byte> ivSnapshot = stackalloc byte[4];
        lock (_sendIvLock)
        {
            _sendIv.AsSpan().CopyTo(ivSnapshot);
        }
        var headerBytes = new byte[PacketCipher.HeaderSize];
        PacketCipher.BuildHeader(body.Length, ivSnapshot, headerBytes);

        lock (_sendIvLock)
        {
            PacketCipher.EncryptBody(body, _sendIv);
        }

        var memory = _writer.GetMemory(PacketCipher.HeaderSize + body.Length);
        headerBytes.CopyTo(memory[..PacketCipher.HeaderSize]);
        body.CopyTo(memory[PacketCipher.HeaderSize..(PacketCipher.HeaderSize + body.Length)]);
        _writer.Advance(PacketCipher.HeaderSize + body.Length);
        _ = _writer.FlushAsync();
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        Exception? terminalError = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _reader!.ReadAsync(ct);
                var buffer = result.Buffer;

                while (!buffer.IsEmpty)
                {
                    SequencePosition? consumed = TryConsumeOne(buffer);
                    if (consumed is null)
                    {
                        break; // need more bytes
                    }
                    buffer = buffer.Slice(consumed.Value);
                }

                _reader.AdvanceTo(buffer.Start, buffer.End);
                if (result.IsCompleted && buffer.IsEmpty)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            terminalError = ex;
            _logger.LogWarning(ex, "Read loop terminated");
        }
        finally
        {
            try
            {
                await _reader!.CompleteAsync();
            }
            catch
            {
                // ignored
            }
            _events.Enqueue(() => Disconnected?.Invoke(terminalError));
        }
    }

    private SequencePosition? TryConsumeOne(ReadOnlySequence<byte> buffer)
    {
        if (!_handshakeComplete)
        {
            return TryConsumeHandshake(buffer);
        }
        return TryConsumePacket(buffer);
    }

    private SequencePosition? TryConsumeHandshake(ReadOnlySequence<byte> buffer)
    {
        var span = buffer.IsSingleSegment ? buffer.FirstSpan : buffer.ToArray();
        if (!HandshakeReader.TryRead(span, out var info, out var consumed))
        {
            return null;
        }
        // Upstream Kinoko PacketChannelInitializer calls
        // LoginPacket.connect(recvIv, sendIv) — i.e. the first IV in the
        // wire is the server's *recvIv*, which by the comment in connect()
        // is "sendIv for client". So the first 4 bytes on the wire are what
        // the client uses to *send*, and the second 4 are what it uses to
        // *receive*. HandshakeReader put the first 4 in info.SendIv and
        // the second 4 in info.RecvIv — wire them through with NO swap.
        Array.Copy(info.SendIv, _sendIv, 4);
        Array.Copy(info.RecvIv, _recvIv, 4);
        Handshake = info;
        _handshakeComplete = true;
        _logger.LogInformation("Handshake complete: version={V} patch={P} locale={L}",
            info.Version, info.Patch, info.Locale);
        _events.Enqueue(() => HandshakeReceived?.Invoke(info));
        return buffer.GetPosition(consumed);
    }

    private SequencePosition? TryConsumePacket(ReadOnlySequence<byte> buffer)
    {
        if (buffer.Length < PacketCipher.HeaderSize)
        {
            return null;
        }
        Span<byte> header = stackalloc byte[PacketCipher.HeaderSize];
        buffer.Slice(0, PacketCipher.HeaderSize).CopyTo(header);
        Span<byte> ivSnapshot = stackalloc byte[4];
        lock (_recvIvLock)
        {
            _recvIv.AsSpan().CopyTo(ivSnapshot);
        }
        var (ok, length) = PacketCipher.ParseHeader(header, ivSnapshot);
        if (!ok)
        {
            _logger.LogError("Bad packet header — closing connection");
            DisposeSocket();
            return buffer.End;
        }
        if (buffer.Length < PacketCipher.HeaderSize + length)
        {
            return null;
        }
        var body = new byte[length];
        buffer.Slice(PacketCipher.HeaderSize, length).CopyTo(body);
        lock (_recvIvLock)
        {
            PacketCipher.DecryptBody(body, _recvIv);
        }
        _inbound.Enqueue(body);
        return buffer.GetPosition(PacketCipher.HeaderSize + length);
    }

    private void DisposeSocket()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }
        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // ignored
        }
        try
        {
            _socket?.Dispose();
        }
        catch
        {
            // ignored
        }
        _stream = null;
        _socket = null;
        _reader = null;
        _writer = null;
        _handshakeComplete = false;
        Handshake = null;
        try
        {
            _cts?.Dispose();
        }
        catch
        {
            // ignored
        }
        _cts = null;
    }

    public void Dispose()
    {
        DisposeSocket();
        GC.SuppressFinalize(this);
    }
}
