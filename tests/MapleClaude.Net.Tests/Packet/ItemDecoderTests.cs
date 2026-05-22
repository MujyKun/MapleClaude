using FluentAssertions;
using MapleClaude.Domain;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

public class ItemDecoderTests
{
    // Encode a bundle item the way upstream Item.encode does, then decode it.
    private static void WriteBundle(OutPacket p, int itemId, short qty, string title = "")
    {
        p.WriteByte((byte)InvItemType.Bundle);  // nType = 2
        p.WriteInt(itemId);
        p.WriteByte(0);                          // cash = false
        p.WriteLong(0);                          // dateExpire (FT)
        p.WriteShort(qty);                       // nNumber
        p.WriteString(title);                    // sTitle
        p.WriteShort(0);                         // nAttribute
    }

    private static void WriteEquip(OutPacket p, int itemId, short str, int durability)
    {
        p.WriteByte((byte)InvItemType.Equip);    // nType = 1
        p.WriteInt(itemId);
        p.WriteByte(0);                          // cash = false
        p.WriteLong(0);                          // dateExpire
        p.WriteByte(7);                          // ruc
        p.WriteByte(0);                          // cuc
        p.WriteShort(str);                       // STR
        for (var i = 0; i < 14; i++) p.WriteShort(0); // DEX..JUMP
        p.WriteString("");                       // title
        p.WriteShort(0);                         // attribute
        p.WriteByte(0);                          // levelUpType
        p.WriteByte(0);                          // level
        p.WriteInt(0);                           // exp
        p.WriteInt(durability);                  // durability
        p.WriteInt(0);                           // iuc
        p.WriteByte(0);                          // grade
        p.WriteByte(0);                          // chuc
        for (var i = 0; i < 5; i++) p.WriteShort(0); // option1..socket2
        p.WriteLong(123456);                     // itemSn (non-cash)
        p.WriteLong(0);                          // ftEquipped
        p.WriteInt(0);                           // prevBonusExpRate
    }

    [Fact]
    public void Decode_Bundle_ReadsQuantity()
    {
        var p = OutPacket.Of((short)OutHeader.InventoryOperation);
        WriteBundle(p, itemId: 2000000, qty: 200, title: "Red Potion");
        var r = new InPacket(p.ToArray());
        r.ReadShort();                           // opcode
        var item = ItemDecoder.Decode(r);
        item.Type.Should().Be(InvItemType.Bundle);
        item.ItemId.Should().Be(2000000);
        item.Quantity.Should().Be(200);
        item.Title.Should().Be("Red Potion");
        item.Equip.Should().BeNull();
        r.Remaining.Should().Be(0);
    }

    [Fact]
    public void Decode_Equip_ReadsStatsAndConsumesExactly()
    {
        var p = OutPacket.Of((short)OutHeader.InventoryOperation);
        WriteEquip(p, itemId: 1302000, str: 5, durability: -1);
        var r = new InPacket(p.ToArray());
        r.ReadShort();                           // opcode
        var item = ItemDecoder.Decode(r);
        item.Type.Should().Be(InvItemType.Equip);
        item.ItemId.Should().Be(1302000);
        item.Equip.Should().NotBeNull();
        item.Equip!.Ruc.Should().Be(7);
        item.Equip.Str.Should().Be(5);
        item.Equip.Durability.Should().Be(-1);
        item.ItemSn.Should().Be(123456);
        r.Remaining.Should().Be(0, "the equip decoder must consume the whole slot exactly");
    }

    [Fact]
    public void InventoryOperation_NewItem_DecodesFullItem()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        List<InventoryOpArg>? captured = null;
        fh.OnInventoryOperation += ops => captured = ops;

        var p = OutPacket.Of((short)OutHeader.InventoryOperation);
        p.WriteByte(0);                          // exclRequest
        p.WriteByte(1);                          // count
        p.WriteByte(0);                          // opType NewItem
        p.WriteByte((byte)InventoryType.Consume);// invType
        p.WriteShort(3);                         // position
        WriteBundle(p, 2000000, 50);             // the item
        p.WriteByte(0);                          // bSN
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        var ops0 = captured!;
        ops0.Should().HaveCount(1);
        var op = ops0[0];
        op.OpType.Should().Be(0);
        op.InvType.Should().Be(InventoryType.Consume);
        op.Pos.Should().Be(3);
        op.Item.Should().NotBeNull();
        op.Item!.ItemId.Should().Be(2000000);
        op.Item.Quantity.Should().Be(50);
    }

    [Fact]
    public void InventoryOperation_Move_DecodesNewPos()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        List<InventoryOpArg>? captured = null;
        fh.OnInventoryOperation += ops => captured = ops;

        var p = OutPacket.Of((short)OutHeader.InventoryOperation);
        p.WriteByte(0);                          // exclRequest
        p.WriteByte(1);                          // count
        p.WriteByte(2);                          // opType Position (move)
        p.WriteByte((byte)InventoryType.Equip);
        p.WriteShort(1);                         // old pos
        p.WriteShort(5);                         // new pos
        p.WriteByte(0);                          // bSN
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

        captured.Should().NotBeNull();
        var ops = captured!;
        ops.Should().HaveCount(1);
        ops[0].OpType.Should().Be(2);
        ops[0].Pos.Should().Be(1);
        ops[0].NewPos.Should().Be(5);
    }

    [Theory]
    [InlineData(InventoryType.Equipped, 0)]
    [InlineData(InventoryType.Equip, 1)]
    [InlineData(InventoryType.Consume, 2)]
    [InlineData(InventoryType.Install, 3)]
    [InlineData(InventoryType.Etc, 4)]
    [InlineData(InventoryType.Cash, 5)]
    public void InventoryType_HasCanonicalValue(InventoryType t, int expected)
    {
        ((int)t).Should().Be(expected);
    }
}
