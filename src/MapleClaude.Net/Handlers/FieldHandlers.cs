using MapleClaude.Domain;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Net.Handlers;

/// <summary>
/// Decoders for the channel-server S→C opcodes we care about in Phase 2.
/// </summary>
public sealed class FieldHandlers
{
    private readonly ILogger<FieldHandlers> _logger;

    public event Action<SetFieldArgs>? OnSetField;

    public FieldHandlers(ILogger<FieldHandlers> logger)
    {
        _logger = logger;
    }

    public void Register(PacketRouter router)
    {
        router.Register(OutHeader.SetField, (p, s) => HandleSetField(p, s));
        router.Register(OutHeader.AliveReq, (p, s) => HandleAliveReq(s));
    }

    private void HandleSetField(InPacket p, ClientSession session)
    {
        _logger.LogInformation("MigrateIn ACK observed — Phase 2 boundary reached");

        // CClientOptMan::DecodeOpt (short 0)
        p.ReadShort();
        var channelId = p.ReadInt();
        var oldDriverId = p.ReadInt();
        var fieldKey = p.ReadByte();
        var isMigrate = p.ReadByte() != 0;
        p.ReadShort(); // nNotifierCheck

        var args = new SetFieldArgs
        {
            ChannelId = channelId,
            FieldKey = fieldKey,
            IsMigrate = isMigrate,
        };

        if (isMigrate)
        {
            args.CalcDamageSeed1 = p.ReadInt();
            args.CalcDamageSeed2 = p.ReadInt();
            args.CalcDamageSeed3 = p.ReadInt();

            try
            {
                // CharacterData::Decode header: long flag, byte (combatOrders), byte (false).
                // The CHARACTER flag bit is always set on a fresh field load, so CharacterStat
                // follows immediately. AvatarLook itself is NOT separately encoded — it's
                // derived from the equipped inventory items (huge structure). For phase-2
                // rendering we only need stat (for position + face/hair/skin/gender) and
                // synthesize a minimal AvatarLook from those fields.
                args.DwFlag = p.ReadLong();
                p.ReadByte(); // nCombatOrders
                p.ReadByte(); // sLinkedCharacter (false)
                args.Stat = AvatarCodec.DecodeCharacterStat(p);
                args.Look = new AvatarLook
                {
                    Gender = args.Stat.Gender,
                    Skin = args.Stat.Skin,
                    Face = args.Stat.Face,
                    Hair = args.Stat.Hair,
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SetField partial decode (only stat needed for render)");
            }
        }
        else
        {
            p.ReadByte(); // isRevive
            args.PosMap = p.ReadInt();
            args.Portal = p.ReadByte();
            p.ReadInt();   // hp
            p.ReadByte();  // bChaseEnable
        }
        OnSetField?.Invoke(args);
    }

    private void HandleAliveReq(ClientSession session)
    {
        var ack = OutPacket.Of(InHeader.AliveAck);
        session.Send(ack);
    }
}

public sealed class SetFieldArgs
{
    public int ChannelId { get; init; }
    public byte FieldKey { get; init; }
    public bool IsMigrate { get; init; }
    public int CalcDamageSeed1 { get; set; }
    public int CalcDamageSeed2 { get; set; }
    public int CalcDamageSeed3 { get; set; }
    public long DwFlag { get; set; }
    public CharacterStat? Stat { get; set; }
    public AvatarLook? Look { get; set; }
    public int PosMap { get; set; }
    public byte Portal { get; set; }
}
