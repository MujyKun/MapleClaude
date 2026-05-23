using FluentAssertions;
using MapleClaude.Domain;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Senders;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

/// <summary>
/// Phase 21 messenger wire tests: the Messenger(143) encoders and the
/// Messenger(372) decode. Field orders mirror upstream Kinoko
/// UserHandler.handleMessenger / MessengerPacket / MessengerUser.encode.
/// </summary>
public class MessengerTests
{
    private static (FieldHandlers fh, PacketRouter router) NewHandlers()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        return (fh, router);
    }

    // ── Opcodes ──────────────────────────────────────────────────────────────────

    [Fact]
    public void MessengerOpcodes_HaveCanonicalValues()
    {
        ((int)InHeader.Messenger).Should().Be(143);
        ((int)OutHeader.Messenger).Should().Be(372);
    }

    // ── Encoders ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MessengerEnter_Encodes_TypeAndId()
    {
        var p = new InPacket(GameSender.MessengerEnter(0).ToArray());
        p.ReadShort().Should().Be((short)InHeader.Messenger);
        p.ReadByte().Should().Be(0);   // Enter
        p.ReadInt().Should().Be(0);    // messengerId (create)
        p.Remaining.Should().Be(0);
    }

    [Fact]
    public void MessengerLeave_Encodes_TypeOnly()
    {
        var p = new InPacket(GameSender.MessengerLeave().ToArray());
        p.ReadShort().Should().Be((short)InHeader.Messenger);
        p.ReadByte().Should().Be(2);   // Leave
        p.Remaining.Should().Be(0);
    }

    [Fact]
    public void MessengerInvite_Encodes_TypeAndName()
    {
        var p = new InPacket(GameSender.MessengerInvite("Buddy").ToArray());
        p.ReadShort().Should().Be((short)InHeader.Messenger);
        p.ReadByte().Should().Be(3);   // Invite
        p.ReadString().Should().Be("Buddy");
        p.Remaining.Should().Be(0);
    }

    [Fact]
    public void MessengerChat_Encodes_TypeAndText()
    {
        var p = new InPacket(GameSender.MessengerChat("hello there").ToArray());
        p.ReadShort().Should().Be((short)InHeader.Messenger);
        p.ReadByte().Should().Be(6);   // Chat
        p.ReadString().Should().Be("hello there");
        p.Remaining.Should().Be(0);
    }

    // ── Decode ───────────────────────────────────────────────────────────────────

    private static AvatarLook SampleLook() => new()
    {
        Gender = 0, Skin = 0, Face = 20000, Hair = 30000, WeaponStickerId = 0,
    };

    [Fact]
    public void Messenger_Enter_DecodesParticipantAcrossAvatar()
    {
        var (fh, router) = NewHandlers();
        MessengerResultArgs? captured = null;
        fh.OnMessengerResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.Messenger);
        p.WriteByte(0);                          // Enter
        p.WriteByte(1);                          // userIndex
        AvatarCodec.EncodeAvatarLook(p, SampleLook());
        p.WriteString("Partner");                // name (after the avatar)
        p.WriteByte(2);                          // channel
        p.WriteByte(1);                          // isNew (bool → byte != 0)
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        captured!.Action.Should().Be(0);
        captured.UserIndex.Should().Be(1);
        captured.Name.Should().Be("Partner");
        captured.Channel.Should().Be(2);
        captured.Flag.Should().BeTrue();
    }

    [Fact]
    public void Messenger_Chat_DecodesText()
    {
        var (fh, router) = NewHandlers();
        MessengerResultArgs? captured = null;
        fh.OnMessengerResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.Messenger);
        p.WriteByte(6);                          // Chat
        p.WriteString("hi!");
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        captured!.Chat.Should().Be("hi!");
    }

    [Fact]
    public void Messenger_Invite_DecodesInviterAndId()
    {
        var (fh, router) = NewHandlers();
        MessengerResultArgs? captured = null;
        fh.OnMessengerResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.Messenger);
        p.WriteByte(3);                          // Invite
        p.WriteString("Inviter");
        p.WriteByte(1);                          // channel
        p.WriteInt(5550);                        // dwSN
        p.WriteByte(0);                          // admin
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        captured!.Name.Should().Be("Inviter");
        captured.MessengerId.Should().Be(5550);
    }

    [Fact]
    public void Messenger_SelfEnterResult_DecodesIndex()
    {
        var (fh, router) = NewHandlers();
        MessengerResultArgs? captured = null;
        fh.OnMessengerResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.Messenger);
        p.WriteByte(1);                          // SelfEnterResult
        p.WriteByte(0);                          // userIndex
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        captured!.Action.Should().Be(1);
        captured.UserIndex.Should().Be(0);
    }
}
