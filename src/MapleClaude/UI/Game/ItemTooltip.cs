using MapleClaude.Character;
using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace MapleClaude.UI.Game;

/// <summary>
/// Item tooltip card, styled after the requested reference (the WzComparerR-style preview): a blue
/// gradient frame with a bulleted name, the full requirement block (REQ LEV/STR/DEX/INT/LUK/FAM +
/// ITEM LEV/EXP), a row of job-requirement pills (BEGINNER … PIRATE), bulleted category / stat /
/// upgrade / hammer lines, the item description, the sell price (with a gold coin) and the item id.
/// Cash items get a gold coin overlaid on the icon (here and in the inventory/equip slots via
/// <see cref="DrawCashCoin"/>).
///
/// Requirement values turn red when the player can't meet them; the player's stats arrive via
/// <see cref="SetPlayer"/> and the description text via a String.wz lookup delegate.
/// </summary>
public sealed class ItemTooltip
{
    private readonly BuiltInFont _font;
    private readonly BuiltInFont _tabFont;   // tiny font for the job-requirement tabs (drawn at scale 1.0)
    private readonly ItemIconLoader _icons;
    private readonly Func<int, string?>? _descOf;

    private static readonly Color FrameTop = new(60, 92, 156, 245);
    private static readonly Color FrameBot = new(20, 34, 70, 245);
    private static readonly Color Border   = new(150, 180, 225);
    private static readonly Color NameColor = Color.White;
    private static readonly Color ReqLabel = new(180, 192, 214);
    private static readonly Color ReqVal   = new(235, 240, 250);
    private static readonly Color ReqBad   = new(235, 80, 80);
    private static readonly Color Dash     = new(120, 130, 150);
    private static readonly Color StatColor = new(205, 216, 236);
    private static readonly Color InfoColor = new(170, 184, 210);
    private static readonly Color IdColor   = new(150, 165, 195);
    private static readonly Color DescColor = new(195, 205, 225);
    private static readonly Color DividerC  = new(90, 110, 150, 160);
    private static readonly Color PillOn    = new(196, 60, 60);    // job tab: class can wear the item
    private static readonly Color PillOff   = new(58, 44, 50);     // job tab: greyed-out
    private static readonly Color PillTxtOn = new(250, 240, 240);
    private static readonly Color PillTxtOff = new(120, 108, 110);

    // Job-class labels for the requirement tabs (drawn very small, the native uses small icons).
    private static readonly string[] JobNames = { "BEGINNER", "WARRIOR", "MAGICIAN", "BOWMAN", "THIEF", "PIRATE" };

    private int _pLevel, _pStr, _pDex, _pInt, _pLuk;

    public ItemTooltip(BuiltInFont font, ItemIconLoader icons, Func<int, string?>? descOf = null,
        BuiltInFont? tabFont = null)
    {
        _font = font;
        _tabFont = tabFont ?? font;
        _icons = icons;
        _descOf = descOf;
    }

    // jobId is accepted for call-site symmetry with the other stats; the job tabs key off the item's
    // own requirement mask, not the player's class, so it isn't stored.
    public void SetPlayer(int level, int str, int dex, int intt, int luk, int jobId)
    {
        _ = jobId;
        _pLevel = level; _pStr = str; _pDex = dex; _pInt = intt; _pLuk = luk;
    }

    public void Draw(SpriteBatch sb, Texture2D white, int itemId, string name, int grade, int quantity,
        int mouseX, int mouseY, int viewW, int viewH)
    {
        var lh = _font.LineHeight;
        var lineStep = Math.Max(lh - 3, 8);     // tight row spacing (less gap above/below each line)
        var attr = _icons.LoadAttr(itemId);
        var isEquip = attr is { IsEquip: true } || itemId / 1_000_000 == 1;
        const int pad = 7, iconBox = 64, iconGap = 8;   // large preview icon
        // Job-tab boxes hug the text: the font's LineHeight bakes in leading/descent the all-caps labels
        // don't use, so the box is shrunk and the glyph cell is centered within it (near-zero top/bottom gap).
        var tabH = Math.Max(8, _tabFont.LineHeight - 4);
        var tabTextY = (tabH - _tabFont.LineHeight) / 2;   // negative: lifts the glyph cell into the short box

        // ── Requirement rows (equips): label + value text + unmet flag. Drawn in two columns to the
        // right of the icon so the block is short and fills the otherwise-empty top-right space. ─────
        var mask = attr?.ReqJob ?? 0;
        var reqs = new List<(string label, string val, bool bad)>();
        if (isEquip)
        {
            var a = attr ?? new ItemAttr();
            reqs.Add(("REQ LEV", a.ReqLevel.ToString(), _pLevel > 0 && _pLevel < a.ReqLevel));
            reqs.Add(("REQ STR", a.ReqStr.ToString(), Unmet(_pStr, a.ReqStr)));
            reqs.Add(("REQ DEX", a.ReqDex.ToString(), Unmet(_pDex, a.ReqDex)));
            reqs.Add(("REQ INT", a.ReqInt.ToString(), Unmet(_pInt, a.ReqInt)));
            reqs.Add(("REQ LUK", a.ReqLuk.ToString(), Unmet(_pLuk, a.ReqLuk)));
            reqs.Add(("REQ FAM", a.ReqFame > 0 ? a.ReqFame.ToString() : "-", false));
            reqs.Add(("ITEM LEV", "-", false));
            reqs.Add(("ITEM EXP", "-", false));
        }
        // Job-requirement tabs: which classes can wear the item (mask 1 War 2 Mag 4 Bow 8 Thf 16 Pir;
        // 0 = any). Index 0 is Beginner (only "any" items).
        var jobOk = new bool[6];
        if (mask <= 0) for (var i = 0; i < 6; i++) jobOk[i] = true;
        else { jobOk[1] = (mask & 1) != 0; jobOk[2] = (mask & 2) != 0; jobOk[3] = (mask & 4) != 0; jobOk[4] = (mask & 8) != 0; jobOk[5] = (mask & 16) != 0; }
        var reqRows = reqs.Count;   // single column: all reqs stacked on the left, beside the icon

        // ── Bulleted info lines ────────────────────────────────────────────────────
        var info = new List<(string text, Color c)>();
        if (isEquip)
        {
            var cat = Category(itemId);
            if (cat.Length > 0) info.Add(($"· CATEGORY : {cat}", InfoColor));
            if (attr != null)
            {
                AddStat(info, "STR", attr.IncStr); AddStat(info, "DEX", attr.IncDex);
                AddStat(info, "INT", attr.IncInt); AddStat(info, "LUK", attr.IncLuk);
                AddStat(info, "Weapon Atk.", attr.IncPad); AddStat(info, "Magic Atk.", attr.IncMad);
                AddStat(info, "Weapon Def.", attr.IncPdd); AddStat(info, "Magic Def.", attr.IncMdd);
                AddStat(info, "MaxHP", attr.IncMhp); AddStat(info, "MaxMP", attr.IncMmp);
                AddStat(info, "Accuracy", attr.IncAcc); AddStat(info, "Avoidability", attr.IncEva);
                AddStat(info, "Speed", attr.IncSpeed); AddStat(info, "Jump", attr.IncJump);
                if (attr.AttackSpeed > 0) info.Add(($"· ATK SPEED : {SpeedLabel(attr.AttackSpeed)}", StatColor));
                info.Add(($"· UPGRADES AVAILABLE : {attr.Upgrades}", InfoColor));
                info.Add(("· VICIOUS HAMMER APPLIED : 0", InfoColor));
            }
        }

        // ── Description ─────────────────────────────────────────────────────────────
        const int wrapW = 250;
        var desc = _descOf?.Invoke(itemId);
        var descLines = string.IsNullOrEmpty(desc) ? new List<string>() : Wrap(desc, wrapW);

        var price = attr?.Price ?? 0;
        var idLine = $"Item ID : {itemId}";

        // ── Measure ─────────────────────────────────────────────────────────────────
        const int pillPad = 1, pillGap = 1, tabTrack = -2;   // tabTrack tightens letter spacing
        var dot = 5;
        var nameW = dot + 3 + (int)_font.Measure(name).X;
        // Single column of reqs with the value column aligned: widest "LABEL :" + a gap + widest value.
        var labelW = 0; var valW = 0;
        foreach (var (label, val, _) in reqs)
        {
            labelW = Math.Max(labelW, (int)_font.Measure($"{label} :").X);
            valW   = Math.Max(valW, (int)_font.Measure(val).X);
        }
        const int valGap = 5;
        var blockW = reqs.Count == 0 ? iconBox : iconBox + iconGap + labelW + valGap + valW;
        var pillsW = isEquip ? JobNames.Sum(p => (int)_tabFont.Measure(p).X + tabTrack * p.Length + pillPad * 2 + pillGap) - pillGap : 0;
        var infoW = info.Count == 0 ? 0 : info.Max(t => (int)_font.Measure(t.text).X);
        var descW = descLines.Count == 0 ? 0 : descLines.Max(s => (int)_font.Measure(s).X);
        var idW   = (int)_font.Measure(idLine).X;
        var contentW = new[] { nameW, blockW, pillsW, infoW, descW, idW, 120 }.Max();
        var w = contentW + pad * 2;

        // Height (tight line spacing throughout).
        var priceH = price > 0 ? lineStep + 1 : 0;
        var leftColH = iconBox + priceH;
        var blockH = Math.Max(leftColH, reqRows * lineStep);
        var h = pad + lh + 2 + Div() + blockH;          // name + divider + (icon | 2-col reqs)
        if (isEquip) h += 5 + tabH;                      // job-requirement tabs (gap above + box)
        h += Div() + info.Count * lineStep;             // info section
        if (descLines.Count > 0) h += Div() + descLines.Count * lineStep;
        h += Div() + lineStep + pad;                    // item id

        var x = mouseX + 16;
        var y = mouseY + 16;
        if (x + w > viewW) x = Math.Max(0, mouseX - w - 4);
        if (y + h > viewH) y = Math.Max(0, viewH - h);

        // ── Frame (blue vertical gradient + rounded border) ─────────────────────────
        for (var i = 0; i < h; i++)
            sb.Draw(white, new Rectangle(x, y + i, w, 1), Color.Lerp(FrameTop, FrameBot, i / (float)Math.Max(1, h - 1)));
        DrawRoundBorder(sb, white, new Rectangle(x, y, w, h), Border);

        var cy = y + pad;

        // Name with a bullet dot.
        sb.Draw(white, new Rectangle(x + pad, cy + lh / 2 - 2, 3, 3), new Color(220, 230, 250));
        _font.Draw(sb, name, new Vector2(x + pad + dot + 3, cy), grade > 0 ? GradeColor(grade) : NameColor);
        cy += lh + 2;
        cy = Divline(sb, white, x, cy, w, pad);

        // Icon (enlarged preview) + requirement block.
        var blockTop = cy;
        var iconRect = new Rectangle(x + pad, blockTop, iconBox, iconBox);
        sb.Draw(white, iconRect, new Color(0, 0, 0, 90));
        DrawBorder(sb, white, iconRect, new Color(110, 130, 165));
        var icon = _icons.LoadIcon(itemId);
        if (icon != null)
        {
            // Scale the icon up to fill the box (item icons are ~32px) so it's actually readable on hover.
            var scale = Math.Min(2.4f, (iconBox - 6f) / Math.Max(icon.Width, icon.Height));
            var dw = (int)(icon.Width * scale); var dh = (int)(icon.Height * scale);
            sb.Draw(icon.Texture, new Rectangle(iconRect.X + (iconBox - dw) / 2, iconRect.Y + (iconBox - dh) / 2, dw, dh), Color.White);
        }
        if (attr is { Cash: true }) DrawCashCoin(sb, white, iconRect.X + 1, iconRect.Bottom - 11);

        if (price > 0)
        {
            DrawCashCoin(sb, white, x + pad, blockTop + iconBox + 1);
            _font.Draw(sb, price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                new Vector2(x + pad + 13, blockTop + iconBox + 1), new Color(235, 215, 120));
        }

        var rxL = x + pad + iconBox + iconGap;
        var valX = rxL + labelW + valGap;   // aligned value column
        var reqTop = blockTop + Math.Max(0, (iconBox - reqRows * lineStep) / 2);   // centre rows beside the icon
        for (var i = 0; i < reqs.Count; i++)
        {
            var (label, val, bad) = reqs[i];
            var ry = reqTop + i * lineStep;
            _font.Draw(sb, $"{label} :", new Vector2(rxL, ry), ReqLabel);
            _font.Draw(sb, val, new Vector2(valX, ry), val == "-" ? Dash : (bad ? ReqBad : ReqVal));
        }
        cy = blockTop + blockH;

        // Job-requirement tabs: the six classes spelled out in a very small font, in tight little boxes —
        // the ones that can wear the item shown bright and the rest greyed (native uses small icons).
        if (isEquip)
        {
            cy += 5;   // drop the tab row down a bit: more gap above (from ITEM EXP), less below (to category)
            var px = x + pad;
            for (var i = 0; i < 6; i++)
            {
                var tw = (int)_tabFont.Measure(JobNames[i]).X + tabTrack * JobNames[i].Length;
                var pw = tw + pillPad * 2;
                sb.Draw(white, new Rectangle(px, cy, pw, tabH), jobOk[i] ? PillOn : PillOff);
                _tabFont.Draw(sb, JobNames[i], new Vector2(px + pillPad, cy + tabTextY), jobOk[i] ? PillTxtOn : PillTxtOff, 1f, tabTrack);
                px += pw + pillGap;
            }
            cy += tabH;
        }

        // Info (category / stats / upgrades / hammer).
        cy = Divline(sb, white, x, cy, w, pad);
        foreach (var (text, c) in info) { _font.Draw(sb, text, new Vector2(x + pad, cy), c); cy += lineStep; }

        // Description.
        if (descLines.Count > 0)
        {
            cy = Divline(sb, white, x, cy, w, pad);
            foreach (var s in descLines) { _font.Draw(sb, s, new Vector2(x + pad, cy), DescColor); cy += lineStep; }
        }

        // Item id.
        cy = Divline(sb, white, x, cy, w, pad);
        _font.Draw(sb, idLine, new Vector2(x + pad, cy), IdColor);
    }

    // ── Cash coin (also used by the inventory/equip slot renderers) ──────────────────
    public static void DrawCashCoin(SpriteBatch sb, Texture2D white, int x, int y)
    {
        const int r = 5;
        int cx = x + r, cy = y + r;
        FillDisc(sb, white, cx, cy, r, new Color(168, 116, 16));       // rim
        FillDisc(sb, white, cx, cy, r - 1, new Color(255, 205, 50));   // gold
        sb.Draw(white, new Rectangle(cx - 2, cy - 3, 2, 2), new Color(255, 242, 175)); // highlight
    }

    private static void FillDisc(SpriteBatch sb, Texture2D white, int cx, int cy, int r, Color c)
    {
        for (var dy = -r; dy <= r; dy++)
        {
            var dx = (int)Math.Sqrt(Math.Max(0, r * r - dy * dy));
            sb.Draw(white, new Rectangle(cx - dx, cy + dy, 2 * dx + 1, 1), c);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────
    private static bool Unmet(int have, int req) => have > 0 && have < req;
    private static int Div() => 5;

    private int Divline(SpriteBatch sb, Texture2D white, int x, int y, int w, int pad)
    {
        sb.Draw(white, new Rectangle(x + pad, y + 2, w - pad * 2, 1), DividerC);
        return y + 5;
    }

    private static void AddStat(List<(string, Color)> lines, string label, int v)
    {
        if (v != 0) lines.Add(($"· {label} : +{v}", StatColor));
    }

    private List<string> Wrap(string text, int maxW)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\\r", "\n").Replace("\\n", "\n").Replace("\r", "").Split('\n'))
        {
            var cur = "";
            foreach (var word in paragraph.Split(' '))
            {
                var trial = cur.Length == 0 ? word : cur + " " + word;
                if (_font.Measure(trial).X > maxW && cur.Length > 0) { lines.Add(cur); cur = word; }
                else cur = trial;
            }
            lines.Add(cur);
        }
        return lines;
    }

    private static Color GradeColor(int g) => g switch
    {
        1 => new Color(0x77, 0xCC, 0xFF), 2 => new Color(0xCC, 0x88, 0xFF),
        3 => new Color(0xFF, 0xCC, 0x33), 4 => new Color(0x55, 0xEE, 0x77),
        _ => Color.White,
    };

    private static string SpeedLabel(int s) => s switch
    {
        <= 2 => $"Faster ({s})", <= 4 => $"Fast ({s})", 5 => $"Normal ({s})", 6 => $"Slow ({s})", _ => $"Slower ({s})",
    };

    private static string Category(int itemId) => (itemId / 10000) switch
    {
        100 => "Hat", 101 => "Face Accessory", 102 => "Eye Accessory", 103 => "Earring",
        104 => "Top", 105 => "Overall", 106 => "Bottom", 107 => "Shoes", 108 => "Glove",
        109 => "Shield", 110 => "Cape", 111 => "Ring", 112 => "Pendant", 113 => "Belt",
        114 => "Medal", 115 => "Shoulder",
        130 => "One-Handed Sword", 131 => "One-Handed Axe", 132 => "One-Handed Blunt Weapon",
        133 => "Dagger", 137 => "Wand", 138 => "Staff",
        140 => "Two-Handed Sword", 141 => "Two-Handed Axe", 142 => "Two-Handed Blunt Weapon",
        143 => "Spear", 144 => "Pole Arm", 145 => "Bow", 146 => "Crossbow", 147 => "Claw",
        148 => "Knuckle", 149 => "Gun",
        _ => string.Empty,
    };

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    // Border with the 4 corner pixels skipped (a subtle rounded look).
    private static void DrawRoundBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X + 1, r.Y, r.Width - 2, 1), c);
        sb.Draw(white, new Rectangle(r.X + 1, r.Bottom - 1, r.Width - 2, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y + 1, 1, r.Height - 2), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y + 1, 1, r.Height - 2), c);
    }
}
