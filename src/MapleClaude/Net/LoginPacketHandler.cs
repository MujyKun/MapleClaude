using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net;

/// <summary>
/// Stage-owned subset of login-server handlers. Today it surfaces just the
/// <c>CreateNewCharacterResult(14)</c> and <c>AliveReq(17)</c> opcodes needed
/// by the character-creation flow.
///
/// Wire format mirrors upstream Kinoko
/// <c>kinoko/packet/stage/LoginPacket.createNewCharacterResult</c>:
/// <c>byte resultCode</c>, on success followed by a serialized
/// <c>CharacterStat</c> + <c>AvatarLook</c> block. We only need the new
/// character id on success, so we peek the result byte and reuse
/// <see cref="AvatarCodec"/> when delving into the success path.
///
/// The long-lived <c>MapleClaude.Net.Handlers.LoginHandlers</c> registered at
/// startup is preserved: <see cref="RegisterAll"/> wraps any prior router
/// registration so both handlers fire on every packet.
/// </summary>
public sealed class LoginPacketHandler
{
    private readonly ILogger<LoginPacketHandler> _logger;
    private readonly Dictionary<OutHeader, IPacketHandler?> _priorHandlers = new();

    public Action<int>? OnCharCreated;
    public Action<string>? OnCharCreateFail;
    public Action? AliveAckRequested;

    public LoginPacketHandler(ILogger<LoginPacketHandler> logger, ILoggerFactory _ = null!)
    {
        _logger = logger;
    }

    public void RegisterAll(ClientSession session)
    {
        var router = session.PacketRouter;
        Wrap(router, OutHeader.CreateNewCharacterResult, HandleCreateNewCharacterResult);
        Wrap(router, OutHeader.AliveReq,                 _ => AliveAckRequested?.Invoke());
    }

    public void UnregisterAll(ClientSession session)
    {
        var router = session.PacketRouter;
        foreach (var (header, prior) in _priorHandlers)
        {
            if (prior is null)
            {
                router.Unregister(header);
            }
            else
            {
                router.Register(header, prior);
            }
        }
        _priorHandlers.Clear();
    }

    private void Wrap(PacketRouter router, OutHeader header, Action<InPacket> ours)
    {
        var prior = router.GetHandler(header);
        _priorHandlers[header] = prior;
        router.Register(header, (packet, session) =>
        {
            if (prior is not null)
            {
                var clone = packet.Clone();
                clone.Skip(2); // re-skip the opcode the router consumed
                try { prior.Handle(clone, session); }
                catch (Exception ex) { _logger.LogError(ex, "Wrapped prior handler for {Header} threw", header); }
            }
            try { ours(packet); }
            catch (Exception ex) { _logger.LogError(ex, "LoginPacketHandler decoder for {Header} threw", header); }
        });
    }

    private void HandleCreateNewCharacterResult(InPacket p)
    {
        var code = p.ReadByte();
        if (code != 0)
        {
            OnCharCreateFail?.Invoke($"Character creation failed (code {code}).");
            return;
        }
        // On success, the upstream encoder writes a CharacterStat first. The
        // very first int field of CharacterStat is the dwId (character id).
        try
        {
            var stat = AvatarCodec.DecodeCharacterStat(p);
            OnCharCreated?.Invoke(stat.CharacterId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CreateNewCharacterResult success path decode failed; raising with id=0");
            OnCharCreated?.Invoke(0);
        }
    }
}
