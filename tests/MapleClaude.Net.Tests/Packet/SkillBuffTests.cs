using FluentAssertions;
using MapleClaude.Domain;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class SkillBuffTests
{
    [Fact]
    public void ChangeSkillRecordResult_DecodesRecords()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        List<SkillRecord>? captured = null;
        fh.OnSkillRecordResult += r => captured = r;

        var p = OutPacket.Of((short)OutHeader.ChangeSkillRecordResult);
        p.WriteByte(0);            // exclRequest
        p.WriteShort(2);           // count
        p.WriteInt(1001003);       // skillId
        p.WriteInt(3);             // level
        p.WriteInt(0);             // masterLevel
        p.WriteLong(0);            // expire (FT)
        p.WriteInt(1001005);
        p.WriteInt(1);
        p.WriteInt(0);
        p.WriteLong(0);
        p.WriteByte(0);            // bSN
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        var recs = captured!;
        recs.Should().HaveCount(2);
        recs[0].SkillId.Should().Be(1001003);
        recs[0].Level.Should().Be(3);
        recs[1].SkillId.Should().Be(1001005);
        recs[1].Level.Should().Be(1);
    }

    [Fact]
    public void TemporaryStatReset_FiresEvent()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        var fired = false;
        fh.OnTemporaryStatReset += () => fired = true;

        var p = OutPacket.Of((short)OutHeader.TemporaryStatReset);
        p.WriteBytes(new byte[16]); // 128-bit flag
        p.WriteByte(0);             // bSN
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        fired.Should().BeTrue();
    }

    [Theory]
    [InlineData(OutHeader.TemporaryStatSet, 31)]
    [InlineData(OutHeader.TemporaryStatReset, 32)]
    [InlineData(OutHeader.ChangeSkillRecordResult, 35)]
    [InlineData(OutHeader.SkillUseResult, 36)]
    [InlineData(OutHeader.UserEffectRemote, 224)]
    [InlineData(OutHeader.UserEffectLocal, 233)]
    public void SkillBuffOpcodes_HaveCanonicalValues(OutHeader op, int expected)
    {
        ((int)op).Should().Be(expected);
    }

    [Theory]
    [InlineData(InHeader.UserSkillUpRequest, 102)]
    [InlineData(InHeader.UserSkillUseRequest, 103)]
    [InlineData(InHeader.UserSkillCancelRequest, 104)]
    [InlineData(InHeader.UserSkillPrepareRequest, 105)]
    public void SkillRequestOpcodes_HaveCanonicalValues(InHeader op, int expected)
    {
        ((int)op).Should().Be(expected);
    }
}
