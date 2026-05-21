using System.Net;
using MapleClaude.Net.Packet;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net.Session;

/// <summary>
/// Drives the login → channel handoff:
/// disconnect from the login server, open a fresh socket to the channel server,
/// run a fresh handshake, then send <c>MigrateIn(20)</c> with the cached
/// <c>clientKey</c> so the channel server can bind us to the original login.
/// </summary>
public sealed class MigrationCoordinator
{
    private readonly ILogger<MigrationCoordinator> _logger;
    private readonly ClientSession _session;
    private MigrationTarget? _pending;

    /// <summary>
    /// Fired when the first decrypted packet from the channel server arrives —
    /// this is the Phase 1 → Phase 2 boundary log line.
    /// </summary>
    public event Action? Phase2BoundaryReached;

    public bool MigrationActive => _pending != null;

    public MigrationCoordinator(ILogger<MigrationCoordinator> logger, ClientSession session)
    {
        _logger = logger;
        _session = session;
    }

    /// <summary>Cache target + characterId, then disconnect from the login server.</summary>
    public async Task BeginMigrateAsync(byte[] channelHost, ushort channelPort, int characterId)
    {
        if (channelHost is null || channelHost.Length != 4)
        {
            throw new ArgumentException("channelHost must be 4 bytes", nameof(channelHost));
        }

        // SelectCharacterResult ships the host as 4 raw bytes (sin_addr); convert
        // to an IPAddress (which produces the same dotted-quad string).
        var ip = new IPAddress(channelHost);
        _pending = new MigrationTarget(ip.ToString(), channelPort, characterId,
            (byte[])_session.Account.ClientKey.Clone(),
            (byte[])_session.MachineId.Clone());

        _session.HandshakeReceived += OnChannelHandshake;
        _session.Disconnected += OnLoginDisconnect;

        _logger.LogInformation("BeginMigrate → {Ip}:{Port} charId={Cid}", ip, channelPort, characterId);
        await _session.DisconnectAsync();
    }

    private void OnLoginDisconnect(Exception? cause)
    {
        if (_pending is null)
        {
            return;
        }
        _ = cause; // unused — the disconnect was expected, we're migrating
        _session.Disconnected -= OnLoginDisconnect;
        var target = _pending;
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Reconnecting to channel {Ip}:{Port}", target.Host, target.Port);
                await _session.ConnectAsync(target.Host, target.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to channel server");
                _pending = null;
            }
        });
    }

    private void OnChannelHandshake(HandshakeInfo info)
    {
        if (_pending is null)
        {
            return;
        }
        _session.HandshakeReceived -= OnChannelHandshake;
        var target = _pending;

        // Send MigrateIn(20): characterId(int) + machineId(byte[16]) + bool(false) + byte(0) + clientKey(byte[8]).
        var p = OutPacket.Of(InHeader.MigrateIn);
        p.WriteInt(target.CharacterId);
        p.WriteBytes(target.MachineId);
        p.WriteByte(false);
        p.WriteByte(0);
        p.WriteBytes(target.ClientKey);
        _session.SendRaw(p.ToArray());
        p.Release();

        _logger.LogInformation("MigrateIn(20) sent — waiting for channel SetField");

        // Arm the boundary log: fire on the first decrypted packet from the channel
        // by hooking the router via a one-shot SetField handler in FieldStage. The
        // coordinator itself just fires the public event here so any subscriber
        // (FieldStage included) can light up.
        Phase2BoundaryReached?.Invoke();
        _pending = null;
    }

    private sealed record MigrationTarget(
        string Host,
        ushort Port,
        int CharacterId,
        byte[] ClientKey,
        byte[] MachineId);
}
