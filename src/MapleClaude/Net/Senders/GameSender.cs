using MapleClaude.Domain;
using MapleClaude.Net.Packet;

namespace MapleClaude.Net.Senders;

public static class GameSender
{
    public static OutPacket AliveAck()
    {
        return OutPacket.Of(InHeader.AliveAck);
    }

    // ── Inventory ──────────────────────────────────────────────────────────────

    // UserChangeSlotPositionRequest(77): int update_time, byte invType,
    // short oldPos, short newPos, short count.
    // Equip: oldPos = source inventory slot, newPos = NEGATIVE body part.
    // Unequip: oldPos = NEGATIVE body part, newPos = free inventory slot.
    // Drop: newPos = 0.
    public static OutPacket ChangeSlotPosition(InventoryType invType, short oldPos, short newPos, short count)
    {
        var p = OutPacket.Of(InHeader.UserChangeSlotPositionRequest);
        p.WriteInt(0);                       // update_time
        p.WriteByte((byte)invType);
        p.WriteShort(oldPos);
        p.WriteShort(newPos);
        p.WriteShort(count);
        return p;
    }

    // UserStatChangeItemUseRequest(78): int update_time, short pos, int itemId.
    public static OutPacket UseItem(short pos, int itemId)
    {
        var p = OutPacket.Of(InHeader.UserStatChangeItemUseRequest);
        p.WriteInt(0);                       // update_time
        p.WriteShort(pos);
        p.WriteInt(itemId);
        return p;
    }

    // UserDropMoneyRequest(106): int update_time, int amount.
    public static OutPacket DropMoney(int amount)
    {
        var p = OutPacket.Of(InHeader.UserDropMoneyRequest);
        p.WriteInt(0);                       // update_time
        p.WriteInt(amount);
        return p;
    }

    public static OutPacket UserChat(string message, bool shout = false)
    {
        var p = OutPacket.Of(InHeader.UserChat);
        p.WriteString(message);
        p.WriteByte(shout);
        return p;
    }

    public static OutPacket UserAbilityUp(int statType)
    {
        var p = OutPacket.Of(InHeader.UserAbilityUpRequest);
        p.WriteInt(0);   // tickCount
        p.WriteInt(statType);
        return p;
    }

    public static class MapleStat
    {
        public const int Str = 0x40;
        public const int Dex = 0x80;
        public const int Int = 0x200;
        public const int Luk = 0x400;
    }

    public static OutPacket TransferChannel(int channelId)
    {
        var p = OutPacket.Of(InHeader.UserTransferChannelRequest);
        p.WriteByte((byte)channelId);
        return p;
    }

    // UserSelectNpc(63): int objectId, short userX, short userY
    // (upstream UserHandler.handleUserSelectNpc reads the player's position).
    public static OutPacket UserSelectNpc(int npcObjId, short userX, short userY)
    {
        var p = OutPacket.Of(InHeader.UserSelectNpc);
        p.WriteInt(npcObjId);
        p.WriteShort(userX);
        p.WriteShort(userY);
        return p;
    }

    // UserScriptMessageAnswer(65). The echoed msgType MUST equal the type the
    // server last sent (UserHandler.handleUserScriptMessageAnswer looks it up).
    //
    // SAY / SAYIMAGE / ASKYESNO / ASKACCEPT: just byte action.
    //   action -1 = prev, 0 = no/end/dismiss, 1 = yes/next.
    public static OutPacket ScriptAnswerSay(ScriptMessageType type, sbyte action)
    {
        var p = OutPacket.Of(InHeader.UserScriptMessageAnswer);
        p.WriteByte((byte)type);
        p.WriteByte((byte)action);
        return p;
    }

    // ASKMENU / ASKNUMBER / ASKSLIDEMENU: byte action, then (if action==1) int answer.
    //   For ASKMENU the int is the selection index; for ASKNUMBER it's the value.
    public static OutPacket ScriptAnswerNumber(ScriptMessageType type, int answer)
    {
        var p = OutPacket.Of(InHeader.UserScriptMessageAnswer);
        p.WriteByte((byte)type);
        p.WriteByte(1);
        p.WriteInt(answer);
        return p;
    }

    // ASKTEXT / ASKBOXTEXT: byte action, then (if action==1) string answer.
    public static OutPacket ScriptAnswerText(ScriptMessageType type, string answer)
    {
        var p = OutPacket.Of(InHeader.UserScriptMessageAnswer);
        p.WriteByte((byte)type);
        p.WriteByte(1);
        p.WriteString(answer);
        return p;
    }

    // Cancel / dismiss a prompt (action 0) for any ASK* type.
    public static OutPacket ScriptAnswerCancel(ScriptMessageType type)
    {
        var p = OutPacket.Of(InHeader.UserScriptMessageAnswer);
        p.WriteByte((byte)type);
        p.WriteByte(0);
        return p;
    }
}
