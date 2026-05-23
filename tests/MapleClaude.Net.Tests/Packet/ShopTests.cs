using FluentAssertions;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Net.Senders;
using MapleClaude.Net.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MapleClaude.Net.Tests.Packet;

/// <summary>
/// Phase 19 NPC-shop wire-format tests: OpenShopDlg(364) decode (normal +
/// rechargeable items), ShopResult(365), and the UserShopRequest(66) sub-actions.
/// </summary>
public class ShopTests
{
    private static (FieldHandlers fh, PacketRouter router) NewHandlers()
    {
        var fh = new FieldHandlers(NullLogger<FieldHandlers>.Instance);
        var router = new PacketRouter(NullLogger<PacketRouter>.Instance);
        fh.Register(router);
        return (fh, router);
    }

    private static void Dispatch(PacketRouter router, OutPacket p) =>
        router.Dispatch(new InPacket(p.ToArray()), session: null!);

    [Theory]
    [InlineData(OutHeader.OpenShopDlg, 364)]
    [InlineData(OutHeader.ShopResult, 365)]
    public void ShopOutOpcodes_HaveCanonicalValues(OutHeader op, int expected) =>
        ((int)op).Should().Be(expected);

    [Fact]
    public void UserShopRequest_HasCanonicalValue() =>
        ((int)InHeader.UserShopRequest).Should().Be(66);

    // ── OpenShopDlg decode ───────────────────────────────────────────────────────

    [Fact]
    public void OpenShopDlg_Decodes_NormalAndRechargeableItems()
    {
        var (fh, router) = NewHandlers();
        ShopOpenArgs? captured = null;
        fh.OnShopOpen += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.OpenShopDlg);
        p.WriteInt(9_999_999);   // npc id
        p.WriteShort(2);         // count
        // normal item: Red Potion
        WriteHeader(p, 2000000, 50);
        p.WriteShort(1);         // quantity
        p.WriteShort(100);       // max per slot
        // rechargeable item: throwing star (prefix 207)
        WriteHeader(p, 2070000, 700);
        p.WriteDouble(1.5);      // unit price
        p.WriteShort(800);       // max per slot (decoded as quantity)
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.NpcId.Should().Be(9_999_999);
        captured.Items.Should().HaveCount(2);
        captured.Items[0].ItemId.Should().Be(2000000);
        captured.Items[0].Price.Should().Be(50);
        captured.Items[0].Quantity.Should().Be(1);
        captured.Items[1].ItemId.Should().Be(2070000);
        captured.Items[1].Price.Should().Be(700);
        captured.Items[1].Quantity.Should().Be(800);
    }

    private static void WriteHeader(OutPacket p, int itemId, int price)
    {
        p.WriteInt(itemId);
        p.WriteInt(price);
        p.WriteByte(0);   // discount
        p.WriteInt(0);    // token item id
        p.WriteInt(0);    // token price
        p.WriteInt(0);    // item period
        p.WriteInt(0);    // level limited
    }

    // ── ShopResult decode ────────────────────────────────────────────────────────

    [Fact]
    public void ShopResult_ServerMsg_DecodesMessage()
    {
        var (fh, router) = NewHandlers();
        ShopResultArgs? captured = null;
        fh.OnShopResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.ShopResult);
        p.WriteByte(19);          // ServerMsg
        p.WriteByte(1);           // has message
        p.WriteString("Closed for the day.");
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.ResultType.Should().Be(19);
        captured.Message.Should().Be("Closed for the day.");
    }

    [Fact]
    public void ShopResult_BuyNoMoney_DecodesType()
    {
        var (fh, router) = NewHandlers();
        ShopResultArgs? captured = null;
        fh.OnShopResult += a => captured = a;

        var p = OutPacket.Of((short)OutHeader.ShopResult);
        p.WriteByte(2);           // BuyNoMoney
        Dispatch(router, p);

        captured.Should().NotBeNull();
        captured!.ResultType.Should().Be(2);
    }

    // ── UserShopRequest encoders ─────────────────────────────────────────────────

    [Fact]
    public void ShopBuy_Encodes_SlotItemCountPrice()
    {
        var p = new InPacket(GameSender.ShopBuy(shopSlot: 3, itemId: 2000000, count: 5, price: 50).ToArray());
        p.ReadShort().Should().Be((short)InHeader.UserShopRequest);
        p.ReadByte().Should().Be(0);        // Buy
        p.ReadShort().Should().Be(3);
        p.ReadInt().Should().Be(2000000);
        p.ReadShort().Should().Be(5);
        p.ReadInt().Should().Be(50);
        p.Remaining.Should().Be(0);
    }

    [Fact]
    public void ShopSell_Encodes_PosItemCount()
    {
        var p = new InPacket(GameSender.ShopSell(pos: 7, itemId: 4000000, count: 2).ToArray());
        p.ReadShort().Should().Be((short)InHeader.UserShopRequest);
        p.ReadByte().Should().Be(1);        // Sell
        p.ReadShort().Should().Be(7);
        p.ReadInt().Should().Be(4000000);
        p.ReadShort().Should().Be(2);
        p.Remaining.Should().Be(0);
    }

    [Fact]
    public void ShopRecharge_And_Close_Encode()
    {
        var r = new InPacket(GameSender.ShopRecharge(pos: 4).ToArray());
        r.ReadShort().Should().Be((short)InHeader.UserShopRequest);
        r.ReadByte().Should().Be(2);        // Recharge
        r.ReadShort().Should().Be(4);
        r.Remaining.Should().Be(0);

        var c = new InPacket(GameSender.ShopClose().ToArray());
        c.ReadShort().Should().Be((short)InHeader.UserShopRequest);
        c.ReadByte().Should().Be(3);        // Close
        c.Remaining.Should().Be(0);
    }
}
