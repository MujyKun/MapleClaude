using FluentAssertions;
using MapleClaude.Domain;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class AvatarLookTests
{
    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        var look = new AvatarLook
        {
            Gender = 1,
            Skin = 2,
            Face = 21002,
            Hair = 31040 + 3,
            WeaponStickerId = 0,
        };
        look.HairEquip[BodyPartSlot.Cap] = 1002000;
        look.HairEquip[BodyPartSlot.Clothes] = 1041006;
        look.HairEquip[BodyPartSlot.Pants] = 1061008;
        look.HairEquip[BodyPartSlot.Shoes] = 1072005;
        look.HairEquip[BodyPartSlot.Weapon] = 1302000;
        look.UnseenEquip[BodyPartSlot.Cap] = 1003000;
        look.PetIds[0] = 5000001;
        look.PetIds[1] = 0;
        look.PetIds[2] = 0;

        var p = OutPacket.Raw();
        AvatarCodec.EncodeAvatarLook(p, look);
        var bytes = p.ToArray();

        var r = new InPacket(bytes);
        var decoded = AvatarCodec.DecodeAvatarLook(r);
        decoded.Gender.Should().Be(look.Gender);
        decoded.Skin.Should().Be(look.Skin);
        decoded.Face.Should().Be(look.Face);
        decoded.Hair.Should().Be(look.Hair);
        decoded.WeaponStickerId.Should().Be(look.WeaponStickerId);
        decoded.HairEquip.Should().BeEquivalentTo(look.HairEquip);
        decoded.UnseenEquip.Should().BeEquivalentTo(look.UnseenEquip);
        decoded.PetIds.Should().Equal(look.PetIds);
        r.Remaining.Should().Be(0);
    }
}
