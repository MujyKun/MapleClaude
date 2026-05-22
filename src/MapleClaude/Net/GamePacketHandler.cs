using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net;

/// <summary>
/// Stage-owned channel-server packet handler. Subscribes to a curated subset of
/// in-game opcodes (<see cref="OutHeader.StatChanged"/>, <see cref="OutHeader.UserChat"/>,
/// <see cref="OutHeader.FuncKeyMappedInit"/>, <see cref="OutHeader.AliveReq"/>,
/// <see cref="OutHeader.Message"/>) and raises lightweight delegate callbacks
/// the active stage can wire to its UI.
///
/// Wire formats mirror upstream Kinoko byte-for-byte:
/// <list type="bullet">
/// <item><c>kinoko/packet/world/WvsContext.statChanged</c></item>
/// <item><c>kinoko/packet/user/UserPacket.chat</c></item>
/// <item><c>kinoko/packet/field/FieldPacket.funcKeyMappedInit</c></item>
/// <item><c>kinoko/packet/world/MessagePacket.system</c> (and other Message types)</item>
/// </list>
///
/// This handler is designed to coexist with the long-lived
/// <c>MapleClaude.Net.Handlers.FieldHandlers</c>: when <see cref="RegisterAll"/>
/// runs, any previously-registered handler for an opcode we touch is saved and
/// re-dispatched alongside ours, so both sets of subscribers fire on every
/// packet. <see cref="UnregisterAll"/> restores the prior handlers.
/// </summary>
public sealed class GamePacketHandler
{
    private readonly ILogger<GamePacketHandler> _logger;
    private readonly Dictionary<OutHeader, IPacketHandler?> _priorHandlers = new();

    /// <summary>Fired after a <c>StatChanged</c> packet is fully decoded; carries
    /// the accumulated stat snapshot (only mask-set fields are updated, others
    /// preserve prior values).</summary>
    public Action<CharStats>? OnStatChanged;

    /// <summary>Fired after a <c>FuncKeyMappedInit</c> packet is fully decoded;
    /// <see cref="FuncKeyEntries"/> holds the 89-entry array the receiver can feed
    /// into <c>KeyConfig.ApplyServerKeymap</c>.</summary>
    public Action<IReadOnlyList<(int keyIndex, int type, int actionId)>>? OnFuncKeyInit;

    /// <summary>Fired after a server <c>AliveReq</c>. The stage should reply
    /// with <c>AliveAck</c>; <see cref="MapleClaude.Net.Handlers.FieldHandlers"/>
    /// already replies on its own, so this hook is informational.</summary>
    public Action? OnAliveReq;

    /// <summary>Fired after a <c>Message(38)</c> packet: (messageType, text).
    /// Only the trivial <c>System</c>/<c>Notice</c> shapes (byte + string) are
    /// surfaced as the text argument; complex variants (IncEXP, IncMoney,
    /// QuestRecord, etc.) return an empty string and rely on the type byte
    /// for dispatch.</summary>
    public Action<byte, string>? OnSystemMsg;

    /// <summary>
    /// 89-entry slot-indexed map of (type, actionId). Index = keyboard scancode
    /// position; type 0 = unbound. Updated on every <c>FuncKeyMappedInit</c>.
    /// </summary>
    public IReadOnlyList<(int keyIndex, int type, int actionId)>? FuncKeyEntries { get; private set; }

    public GamePacketHandler(ILogger<GamePacketHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe our decoder to the router for every opcode we surface. Any
    /// pre-existing handler for the same opcode is saved and chained — both
    /// run on every packet so global handlers (FieldHandlers) still fire.
    /// </summary>
    public void RegisterAll(ClientSession session)
    {
        var router = session.PacketRouter;
        Wrap(router, OutHeader.StatChanged,        HandleStatChanged);
        // UserChat is handled canonically by FieldHandlers.OnUserChat (which the
        // GameStage feeds into the ChatBar). We no longer wrap it here to avoid
        // a duplicate chat line per inbound message.
        Wrap(router, OutHeader.FuncKeyMappedInit,  HandleFuncKeyMappedInit);
        Wrap(router, OutHeader.AliveReq,           HandleAliveReq);
        Wrap(router, OutHeader.Message,            HandleMessage);
    }

    /// <summary>Symmetric teardown — restores any handlers that were registered
    /// before <see cref="RegisterAll"/> ran. Safe to call from <c>Stage.OnExit</c>.</summary>
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
            // Replay first to the prior handler over a cloned cursor (so it
            // sees the body from offset-after-opcode); our own decoder runs
            // last on the live cursor so unread bytes don't leak.
            if (prior is not null)
            {
                var clone = packet.Clone();
                clone.Skip(2); // PacketRouter consumed the opcode before dispatching to us; restore parity.
                try
                {
                    prior.Handle(clone, session);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wrapped prior handler for {Header} threw", header);
                }
            }
            try
            {
                ours(packet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GamePacketHandler decoder for {Header} threw", header);
            }
        });
    }

    // ── Decoders ──────────────────────────────────────────────────────────────

    // WvsContext.statChanged: byte bExclRequestSent, int dwCharStat,
    //   per-Stat ordered encoding (SKIN, FACE, HAIR, PETSN, LEVEL, JOB, STR, DEX,
    //   INT, LUK, HP, MHP, MP, MMP, AP, SP, EXP, POP, MONEY, PETSN2, PETSN3,
    //   TEMPEXP), trailing byte 0, trailing byte 0.
    //
    // SKIN, LEVEL    -> byte
    // JOB, STR, DEX, INT, LUK, AP, POP -> short
    // FACE, HAIR, HP, MHP, MP, MMP, EXP, MONEY, TEMPEXP -> int
    // PETSN, PETSN2, PETSN3 -> long
    // SP -> short (no extend-SP for v95 explorer classes; safe to read short).
    //
    // Stat bit values, per kinoko/world/user/stat/Stat.java:
    //   SKIN=0x1   FACE=0x2   HAIR=0x4   PETSN=0x8
    //   LEVEL=0x10 JOB=0x20   STR=0x40   DEX=0x80
    //   INT=0x100  LUK=0x200  HP=0x400   MHP=0x800
    //   MP=0x1000  MMP=0x2000 AP=0x4000  SP=0x8000
    //   EXP=0x10000 POP=0x20000 MONEY=0x40000
    //   PETSN2=0x80000 PETSN3=0x100000 TEMPEXP=0x200000
    private void HandleStatChanged(InPacket p)
    {
        p.ReadByte();             // bExclRequestSent
        var mask = p.ReadInt();   // dwCharStat (Stat.from(...))

        var stats = new CharStats
        {
            // Preserve prior known fields by reading any existing snapshot --
            // but since GamePacketHandler holds none, this is the merge surface.
            Name = string.Empty,
        };

        if ((mask & 0x1)     != 0) p.ReadByte();                // SKIN
        if ((mask & 0x2)     != 0) p.ReadInt();                 // FACE
        if ((mask & 0x4)     != 0) p.ReadInt();                 // HAIR
        if ((mask & 0x8)     != 0) p.ReadLong();                // PETSN
        if ((mask & 0x10)    != 0) stats.Level = p.ReadByte();  // LEVEL
        if ((mask & 0x20)    != 0) stats.JobId = p.ReadShort(); // JOB
        if ((mask & 0x40)    != 0) stats.Str   = p.ReadShort();
        if ((mask & 0x80)    != 0) stats.Dex   = p.ReadShort();
        if ((mask & 0x100)   != 0) stats.Int   = p.ReadShort();
        if ((mask & 0x200)   != 0) stats.Luk   = p.ReadShort();
        if ((mask & 0x400)   != 0) stats.Hp    = p.ReadInt();
        if ((mask & 0x800)   != 0) stats.MaxHp = p.ReadInt();
        if ((mask & 0x1000)  != 0) stats.Mp    = p.ReadInt();
        if ((mask & 0x2000)  != 0) stats.MaxMp = p.ReadInt();
        if ((mask & 0x4000)  != 0) stats.AP    = p.ReadShort();
        if ((mask & 0x8000)  != 0) stats.SP    = p.ReadShort();
        if ((mask & 0x10000) != 0) stats.Exp   = p.ReadInt();
        if ((mask & 0x20000) != 0) p.ReadShort();               // POP
        if ((mask & 0x40000) != 0) p.ReadInt();                 // MONEY
        if ((mask & 0x80000) != 0) p.ReadLong();                // PETSN2
        if ((mask & 0x100000)!= 0) p.ReadLong();                // PETSN3
        if ((mask & 0x200000)!= 0) p.ReadInt();                 // TEMPEXP

        // Trailing flags — best-effort: server emits two trailing bytes but
        // we may have consumed them or not depending on cursor; ignore parse
        // errors so a partial body never aborts the stage.
        try { p.ReadByte(); } catch { /* SecondaryStatChangedPoint flag */ }
        try { p.ReadByte(); } catch { /* BattleRecoveryInfo flag */ }

        OnStatChanged?.Invoke(stats);
    }

    // FieldPacket.funcKeyMappedInit (OutHeader.FuncKeyMappedInit = 398):
    //   byte bDefault, then 89 entries of (byte nType, int nID) when !bDefault.
    private void HandleFuncKeyMappedInit(InPacket p)
    {
        var isDefault = p.ReadBool();
        var entries = new List<(int keyIndex, int type, int actionId)>(89);
        if (!isDefault)
        {
            for (var i = 0; i < 89; i++)
            {
                var type     = p.ReadByte();
                var actionId = p.ReadInt();
                entries.Add((i, type, actionId));
            }
        }
        FuncKeyEntries = entries;
        OnFuncKeyInit?.Invoke(entries);
    }

    // AliveReq (OutHeader.AliveReq = 17): empty body. FieldHandlers already
    // replies with AliveAck; this hook is informational.
    private void HandleAliveReq(InPacket _) => OnAliveReq?.Invoke();

    // MessagePacket (OutHeader.Message = 38): leading byte = MessageType,
    // then a type-specific tail. Decode only the simple System(5)/Notice text
    // variants here (byte + string); other variants surface the type byte
    // and an empty string so callers can decide whether to drill in.
    private void HandleMessage(InPacket p)
    {
        var type = p.ReadByte();
        string text;
        try
        {
            // System (5) is `byte + string`. Most other Message subtypes start
            // with non-string payloads (int exp, short questId, byte count, …)
            // so a blind ReadString would mis-decode. Only attempt the string
            // read for System; otherwise emit empty.
            text = type == 5 ? p.ReadString() : string.Empty;
        }
        catch
        {
            text = string.Empty;
        }
        OnSystemMsg?.Invoke(type, text);
    }
}
