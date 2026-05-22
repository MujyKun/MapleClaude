using FluentAssertions;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using MapleClaude.Net.Senders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

/// <summary>
/// Phase 8 social wire-format tests: group chat, whisper, party (the
/// column-major PARTYDATA block), and the friend list. Decoders are exercised
/// through <see cref="PacketRouter"/> exactly as the live pipeline runs them.
/// </summary>
public class SocialTests
{
    private const int PartyMax = 6;

    private static (FieldHandlers fh, PacketRouter router) NewHandlers()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        return (fh, router);
    }

    private static void Dispatch(PacketRouter router, OutPacket p) =>
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

    // ── Opcode values ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(InHeader.GroupMessage, 140)]
    [InlineData(InHeader.Whisper, 141)]
    [InlineData(InHeader.PartyRequest, 145)]
    [InlineData(InHeader.FriendRequest, 153)]
    [InlineData(InHeader.UserChat, 54)]
    public void InboundSocialOpcodes_HaveCanonicalValues(InHeader op, int expected) =>
        ((int)op).Should().Be(expected);

    [Theory]
    [InlineData(OutHeader.PartyResult, 62)]
    [InlineData(OutHeader.FriendResult, 65)]
    [InlineData(OutHeader.GroupMessage, 150)]
    [InlineData(OutHeader.Whisper, 151)]
    [InlineData(OutHeader.UserChat, 181)]
    public void OutboundSocialOpcodes_HaveCanonicalValues(OutHeader op, int expected) =>
        ((int)op).Should().Be(expected);

    // ── GroupMessage S→C ─────────────────────────────────────────────────────────

    [Fact]
    public void GroupMessage_Decodes_TypeFromText()
    {
        var (fh, router) = NewHandlers();
        (int type, string from, string text)? captured = null;
        fh.OnGroupMessage += (t, f, m) => captured = (t, f, m);

        var p = OutPacket.Of((short)OutHeader.GroupMessage);
        p.WriteByte(1);                 // ChatGroupType.Party
        p.WriteString("Alice");         // sFrom
        p.WriteString("hello party");   // sMsg
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.Value.type.Should().Be(1);
        captured.Value.from.Should().Be("Alice");
        captured.Value.text.Should().Be("hello party");
    }

    // ── Whisper S→C ──────────────────────────────────────────────────────────────

    [Fact]
    public void Whisper_Receive_Decodes()
    {
        var (fh, router) = NewHandlers();
        (string from, int ch, string text)? captured = null;
        fh.OnWhisper += (f, c, t) => captured = (f, c, t);

        var p = OutPacket.Of((short)OutHeader.Whisper);
        p.WriteByte(0x12);              // WhisperReceive = Whisper(0x2) | Receive(0x10)
        p.WriteString("Bob");          // sourceName
        p.WriteByte(3);                // nChannelID
        p.WriteByte(0);                // bFromAdmin
        p.WriteString("psst");         // message
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.Value.from.Should().Be("Bob");
        captured.Value.ch.Should().Be(3);
        captured.Value.text.Should().Be("psst");
    }

    [Fact]
    public void Whisper_NonReceiveReply_DoesNotFireOnWhisper()
    {
        var (fh, router) = NewHandlers();
        var fired = false;
        fh.OnWhisper += (_, _, _) => fired = true;

        // WhisperResult = Whisper(0x2) | Result(0x8) = 0x0A — no Receive bit.
        var p = OutPacket.Of((short)OutHeader.Whisper);
        p.WriteByte(0x0A);
        p.WriteString("Carol");        // sReceiver
        p.WriteByte(1);                // success
        Dispatch(router, p);

        fired.Should().BeFalse();
    }

    // ── PartyResult S→C — the column-major PARTYDATA block ───────────────────────

    [Fact]
    public void PartyResult_LoadPartyDone_DecodesMembers_AndBoss()
    {
        var (fh, router) = NewHandlers();
        (List<PartyMember> members, int boss)? captured = null;
        fh.OnPartyLoad += (m, b) => captured = (m, b);

        var members = new[]
        {
            new PdMember { CharId = 1001, Name = "Leader", Job = 112, Level = 75, Channel = 0 },
            new PdMember { CharId = 1002, Name = "Member", Job = 212, Level = 60, Channel = 1 },
        };

        var p = OutPacket.Of((short)OutHeader.PartyResult);
        p.WriteByte(7);                 // PartyResultType.LoadParty_Done
        p.WriteInt(42);                 // nPartyID
        WritePartyData(p, members, partyBossId: 1001);
        Dispatch(router, p);

        captured.Should().NotBeNull();
        var (list, boss) = captured!.Value;
        list.Should().HaveCount(2);     // 4 empty slots (charId 0) are skipped
        boss.Should().Be(1001);

        list[0].CharId.Should().Be(1001);
        list[0].Name.Should().Be("Leader");
        list[0].Job.Should().Be(112);
        list[0].Level.Should().Be(75);
        list[0].Channel.Should().Be(0);

        list[1].CharId.Should().Be(1002);
        list[1].Name.Should().Be("Member");
        list[1].Job.Should().Be(212);
        list[1].Level.Should().Be(60);
        list[1].Channel.Should().Be(1);
    }

    [Fact]
    public void PartyResult_JoinPartyDone_DecodesMembers()
    {
        var (fh, router) = NewHandlers();
        List<PartyMember>? captured = null;
        fh.OnPartyLoad += (m, _) => captured = m;

        var members = new[]
        {
            new PdMember { CharId = 7, Name = "A", Job = 0, Level = 10, Channel = 0 },
            new PdMember { CharId = 8, Name = "B", Job = 0, Level = 12, Channel = 0 },
        };

        var p = OutPacket.Of((short)OutHeader.PartyResult);
        p.WriteByte(15);                // JoinParty_Done
        p.WriteInt(99);                 // nPartyID
        p.WriteString("B");             // joining member name (length-prefixed)
        WritePartyData(p, members, partyBossId: 7);
        Dispatch(router, p);

        captured.Should().NotBeNull();
        var joined = captured!;
        joined.Should().HaveCount(2);
        joined[1].Name.Should().Be("B");
    }

    [Fact]
    public void PartyResult_WithdrawDisband_EmitsEmptyParty()
    {
        var (fh, router) = NewHandlers();
        (List<PartyMember> members, int boss)? captured = null;
        fh.OnPartyLoad += (m, b) => captured = (m, b);

        var p = OutPacket.Of((short)OutHeader.PartyResult);
        p.WriteByte(12);                // WithdrawParty_Done
        p.WriteInt(42);                 // nPartyID
        p.WriteInt(1001);               // leaving charId
        p.WriteByte(0);                 // notDisband = false → party disbanded
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.Value.members.Should().BeEmpty();
    }

    [Fact]
    public void PartyResult_InviteParty_RaisesOnPartyInvite()
    {
        var (fh, router) = NewHandlers();
        (int id, string name)? captured = null;
        fh.OnPartyInvite += (id, name) => captured = (id, name);

        var p = OutPacket.Of((short)OutHeader.PartyResult);
        p.WriteByte(4);                 // InviteParty
        p.WriteInt(555);                // dwInviterID
        p.WriteString("Inviter");       // sInviter
        p.WriteInt(50);                 // nLevel
        p.WriteInt(112);                // nJobCode
        p.WriteByte(0);                 // trailing
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.Value.id.Should().Be(555);
        captured.Value.name.Should().Be("Inviter");
    }

    // ── FriendResult S→C ─────────────────────────────────────────────────────────

    [Fact]
    public void FriendResult_LoadDone_DecodesFriends()
    {
        var (fh, router) = NewHandlers();
        List<FriendInfo>? captured = null;
        fh.OnFriendList += list => captured = list;

        var p = OutPacket.Of((short)OutHeader.FriendResult);
        p.WriteByte(7);                 // LoadFriend_Done
        p.WriteByte(2);                 // count
        // friend 0 — online on channel 0
        p.WriteInt(2001);
        p.WriteString("FriendOne", 13);
        p.WriteByte(0);                 // flag (NORMAL)
        p.WriteInt(0);                  // channel 0 → online
        p.WriteString("Group A", 17);
        // friend 1 — offline (CHANNEL_OFFLINE = -2)
        p.WriteInt(2002);
        p.WriteString("FriendTwo", 13);
        p.WriteByte(0);
        p.WriteInt(-2);                 // offline
        p.WriteString("Group B", 17);
        // aInShop array
        p.WriteInt(0);
        p.WriteInt(0);
        Dispatch(router, p);

        captured.Should().NotBeNull();
        var friends = captured!;
        friends.Should().HaveCount(2);
        friends[0].FriendId.Should().Be(2001);
        friends[0].Name.Should().Be("FriendOne");
        friends[0].Channel.Should().Be(0);
        friends[0].Online.Should().BeTrue();
        friends[1].Name.Should().Be("FriendTwo");
        friends[1].Channel.Should().Be(-2);
        friends[1].Online.Should().BeFalse();
    }

    // ── Encoder shape checks (C→S) ───────────────────────────────────────────────

    [Fact]
    public void PartyInvite_Encodes_TypeAndName()
    {
        var p = new InPacket(GameSender.PartyInvite("Target").ToArray());
        p.ReadShort().Should().Be((short)InHeader.PartyRequest);
        p.ReadByte().Should().Be(4);    // InviteParty
        p.ReadString().Should().Be("Target");
    }

    [Fact]
    public void PartyJoin_Encodes_InviterId_AndTrailingByte()
    {
        var p = new InPacket(GameSender.PartyJoin(1234).ToArray());
        p.ReadShort().Should().Be((short)InHeader.PartyRequest);
        p.ReadByte().Should().Be(3);    // JoinParty
        p.ReadInt().Should().Be(1234);
        p.ReadByte().Should().Be(0);    // trailing byte the server reads
    }

    [Fact]
    public void Whisper_Encodes_FlagUpdateTargetText()
    {
        var p = new InPacket(GameSender.Whisper("Dave", "yo").ToArray());
        p.ReadShort().Should().Be((short)InHeader.Whisper);
        p.ReadByte().Should().Be(0x6); // WhisperRequest = Whisper(0x2) | Request(0x4)
        p.ReadInt().Should().Be(0);    // update_time
        p.ReadString().Should().Be("Dave");
        p.ReadString().Should().Be("yo");
    }

    [Fact]
    public void GroupChat_Encodes_UpdateTypeCountIdsText()
    {
        var ids = new[] { 11, 22, 33 };
        var p = new InPacket(GameSender.GroupChat(GameSender.ChatGroupType.Party, ids, "go").ToArray());
        p.ReadShort().Should().Be((short)InHeader.GroupMessage);
        p.ReadInt().Should().Be(0);    // update_time
        p.ReadByte().Should().Be(1);   // ChatGroupType.Party
        p.ReadByte().Should().Be(3);   // count
        p.ReadInt().Should().Be(11);
        p.ReadInt().Should().Be(22);
        p.ReadInt().Should().Be(33);
        p.ReadString().Should().Be("go");
    }

    [Fact]
    public void FriendAccept_Encodes_TypeAndId()
    {
        var p = new InPacket(GameSender.FriendAccept(4242).ToArray());
        p.ReadShort().Should().Be((short)InHeader.FriendRequest);
        p.ReadByte().Should().Be(2);   // AcceptFriend
        p.ReadInt().Should().Be(4242);
    }

    [Fact]
    public void FriendLoad_Encodes_TypeOnly()
    {
        var p = new InPacket(GameSender.FriendLoad().ToArray());
        p.ReadShort().Should().Be((short)InHeader.FriendRequest);
        p.ReadByte().Should().Be(0);   // LoadFriend
        p.Remaining.Should().Be(0);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────

    private sealed class PdMember
    {
        public int CharId;
        public string Name = string.Empty;
        public int Job;
        public int Level;
        public int Channel;
    }

    // Mirrors upstream kinoko/server/party/Party.encode (PARTYDATA::Decode):
    // every field is a 6-element column written contiguously, in this order:
    //   charId, name(13), job, level, channelId, [partyBossId], fieldId,
    //   townPortal(5 ints), pqReward, pqRewardType, [pqRewardMobTemplateId],
    //   [bPQReward].
    private static void WritePartyData(OutPacket p, IReadOnlyList<PdMember> members, int partyBossId)
    {
        PdMember Slot(int i) => i < members.Count ? members[i] : Empty;

        for (var i = 0; i < PartyMax; i++) p.WriteInt(Slot(i).CharId);
        for (var i = 0; i < PartyMax; i++) p.WriteString(Slot(i).Name, 13);
        for (var i = 0; i < PartyMax; i++) p.WriteInt(Slot(i).Job);
        for (var i = 0; i < PartyMax; i++) p.WriteInt(Slot(i).Level);
        for (var i = 0; i < PartyMax; i++) p.WriteInt(Slot(i).Channel);
        p.WriteInt(partyBossId);
        for (var i = 0; i < PartyMax; i++) p.WriteInt(999999999);   // fieldId (UNDEFINED)
        for (var i = 0; i < PartyMax; i++)                           // TownPortal: 5 ints
        {
            p.WriteInt(999999999); // townId
            p.WriteInt(999999999); // fieldId
            p.WriteInt(0);         // skillId
            p.WriteInt(0);         // x
            p.WriteInt(0);         // y
        }
        for (var i = 0; i < PartyMax; i++) p.WriteInt(0);           // pqReward
        for (var i = 0; i < PartyMax; i++) p.WriteInt(0);           // pqRewardType
        p.WriteInt(0);                                              // pqRewardMobTemplateId
        p.WriteInt(0);                                              // bPQReward
    }

    private static readonly PdMember Empty = new() { CharId = 0, Name = string.Empty };
}
