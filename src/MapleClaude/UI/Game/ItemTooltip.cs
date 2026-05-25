using MapleClaude.Character;
using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;

namespace MapleClaude.UI.Game;

/// <summary>
/// Item tooltip card — a faithful reproduction of the v95 <c>CUIToolTip::SetToolTip_Equip</c> /
/// <c>SetToolTip_Bundle</c> layout. The client draws the tooltip procedurally (see
/// <c>CUIToolTip::InitCanvas</c>: a solid dark fill + white corner pixels + an inner outline — there is
/// NO WZ frame asset) and composes the content with GDI/system fonts in a fixed colour palette
/// (<c>GetFontByType</c>), so this matches the original mechanism (our <see cref="BuiltInFont"/> is the
/// same GDI wrapper):
///
///   • item icon (left) + name centred at the top, coloured by potential grade;
///   • requirement block (REQ LEV / STR / DEX / INT / LUK / JOB) — each value drawn RED when the
///     player can't meet it (<c>DrawTextEquip_Req_Level</c>: <c>cur &lt; req</c> → m_pNumberCannot);
///   • category + attack-speed line;
///   • equip stat-bonus lines (incSTR/incPAD/…);
///   • the item description (word-wrapped).
///
/// The player's req stats are supplied by <see cref="SetPlayer"/> (pushed from the stage); the
/// description text comes from a String.wz lookup delegate.
/// </summary>
public sealed class ItemTooltip
{
    private readonly BuiltInFont _font;
    private readonly ItemIconLoader _icons;
    private readonly Func<int, string?>? _descOf;

    // Potential-grade name colours (rare/epic/unique/legendary) — matches the v95 highlight fonts.
    private static readonly Color[] GradeColor =
    {
        Color.White,                 // 0 none
        new(0x77, 0xCC, 0xFF),       // 1 rare   — blue
        new(0xCC, 0x88, 0xFF),       // 2 epic   — purple
        new(0xFF, 0xCC, 0x33),       // 3 unique — gold
        new(0x55, 0xEE, 0x77),       // 4 legendary — green
        new(0xFF, 0xAA, 0x55),       // 5 — orange
    };
    private static readonly Color BodyColor = new(225, 225, 225);
    private static readonly Color StatColor = new(120, 215, 120);   // equip bonuses (green, v95)
    private static readonly Color ReqOk     = new(204, 204, 204);   // requirement met
    private static readonly Color ReqBad    = new(220, 70, 70);     // requirement not met (red)
    private static readonly Color LabelGray = new(160, 160, 160);
    private static readonly Color DescColor = new(200, 200, 200);
    private static readonly Color Divider   = new(70, 70, 84);
    private static readonly Color BgColor   = new(12, 12, 18, 238);

    // Player req snapshot (0 = unknown → never flags red).
    private int _pLevel, _pStr, _pDex, _pInt, _pLuk, _pJob;

    public ItemTooltip(BuiltInFont font, ItemIconLoader icons, Func<int, string?>? descOf = null)
    {
        _font = font;
        _icons = icons;
        _descOf = descOf;
    }

    /// <summary>Supply the player's requirement-relevant stats so the tooltip can flag unmet reqs red.</summary>
    public void SetPlayer(int level, int str, int dex, int intt, int luk, int jobId)
    {
        _pLevel = level; _pStr = str; _pDex = dex; _pInt = intt; _pLuk = luk; _pJob = jobId;
    }

    public void Draw(SpriteBatch sb, Texture2D white, int itemId, string name, int grade, int quantity,
        int mouseX, int mouseY, int viewW, int viewH)
    {
        var lh = _font.LineHeight;
        var attr = _icons.LoadAttr(itemId);
        var isEquip = attr is { IsEquip: true } || itemId / 1_000_000 == 1;

        // ── Requirement rows (equips only): (label, value-text, unmet) ─────────────
        var reqs = new List<(string label, string val, bool bad)>();
        if (isEquip && attr != null)
        {
            reqs.Add(("REQ LEV", attr.ReqLevel.ToString(), _pLevel > 0 && _pLevel < attr.ReqLevel));
            if (attr.ReqStr > 0) reqs.Add(("REQ STR", attr.ReqStr.ToString(), Unmet(_pStr, attr.ReqStr)));
            if (attr.ReqDex > 0) reqs.Add(("REQ DEX", attr.ReqDex.ToString(), Unmet(_pDex, attr.ReqDex)));
            if (attr.ReqInt > 0) reqs.Add(("REQ INT", attr.ReqInt.ToString(), Unmet(_pInt, attr.ReqInt)));
            if (attr.ReqLuk > 0) reqs.Add(("REQ LUK", attr.ReqLuk.ToString(), Unmet(_pLuk, attr.ReqLuk)));
            reqs.Add(("REQ JOB", JobReq(attr.ReqJob), JobUnmet(attr.ReqJob, _pJob)));
        }

        // ── Category + attack speed ────────────────────────────────────────────────
        var info = new List<string>();
        if (isEquip)
        {
            var cat = Category(itemId);
            if (cat.Length > 0) info.Add($"CATEGORY : {cat}");
            if (attr is { AttackSpeed: > 0 }) info.Add($"ATTACK SPEED : {SpeedLabel(attr.AttackSpeed)}");
            if (attr is { Upgrades: > 0 })    info.Add($"UPGRADES AVAILABLE : {attr.Upgrades}");
        }

        // ── Equip stat-bonus lines ─────────────────────────────────────────────────
        var stats = new List<string>();
        if (isEquip && attr != null)
        {
            AddStat(stats, "STR", attr.IncStr);
            AddStat(stats, "DEX", attr.IncDex);
            AddStat(stats, "INT", attr.IncInt);
            AddStat(stats, "LUK", attr.IncLuk);
            AddStat(stats, "Weapon Atk.", attr.IncPad);
            AddStat(stats, "Magic Atk.", attr.IncMad);
            AddStat(stats, "Weapon Def.", attr.IncPdd);
            AddStat(stats, "Magic Def.", attr.IncMdd);
            AddStat(stats, "MaxHP", attr.IncMhp);
            AddStat(stats, "MaxMP", attr.IncMmp);
            AddStat(stats, "Accuracy", attr.IncAcc);
            AddStat(stats, "Avoidability", attr.IncEva);
            AddStat(stats, "Speed", attr.IncSpeed);
            AddStat(stats, "Jump", attr.IncJump);
        }
        if (!isEquip && quantity > 1) stats.Add($"Quantity : {quantity}");

        // ── Description (word-wrapped) ─────────────────────────────────────────────
        const int wrapW = 210;
        var desc = _descOf?.Invoke(itemId);
        var descLines = string.IsNullOrEmpty(desc) ? new List<string>() : Wrap(desc, wrapW);

        // ── Measure ────────────────────────────────────────────────────────────────
        const int pad = 7, iconBox = 34, iconGap = 6;
        var nameW = (int)_font.Measure(name).X;
        // Requirement block width (label + value, 1 column to the right of the icon).
        var reqW = reqs.Count == 0 ? 0
            : reqs.Max(r => (int)_font.Measure($"{r.label} : {r.val}").X);
        var blockW = reqs.Count == 0 ? 0 : iconBox + iconGap + reqW;
        var infoW  = info.Count  == 0 ? 0 : info.Max(s => (int)_font.Measure(s).X);
        var statW  = stats.Count == 0 ? 0 : stats.Max(s => (int)_font.Measure(s).X);
        var descW  = descLines.Count == 0 ? 0 : descLines.Max(s => (int)_font.Measure(s).X);
        var contentW = Math.Max(Math.Max(nameW, blockW), Math.Max(Math.Max(infoW, statW), descW));
        contentW = Math.Max(contentW, 120);
        var w = contentW + pad * 2;

        // Height: name + divider + block + (divider+info) + (divider+stats) + (divider+desc).
        var blockH = reqs.Count == 0 ? 0 : Math.Max(iconBox, reqs.Count * lh);
        var h = pad + lh + 4 + Sep(reqs.Count) + blockH;
        if (info.Count  > 0) h += Sep(1) + info.Count  * lh;
        if (stats.Count > 0) h += Sep(1) + stats.Count * lh;
        if (descLines.Count > 0) h += Sep(1) + descLines.Count * lh;
        h += pad;

        var x = mouseX + 16;
        var y = mouseY + 16;
        if (x + w > viewW) x = Math.Max(0, mouseX - w - 4);
        if (y + h > viewH) y = Math.Max(0, viewH - h);

        // ── Frame (mirrors CUIToolTip::InitCanvas) ─────────────────────────────────
        var rect = new Rectangle(x, y, w, h);
        sb.Draw(white, rect, BgColor);
        DrawBorder(sb, white, rect, new Color(140, 140, 160));
        // white corner accents
        foreach (var (cx, cy) in new[] { (x, y), (x + w - 1, y), (x, y + h - 1), (x + w - 1, y + h - 1) })
            sb.Draw(white, new Rectangle(cx, cy, 1, 1), Color.White);

        var cy0 = y + pad;

        // Name (centred).
        var nameColor = GradeColor[Math.Clamp(grade, 0, GradeColor.Length - 1)];
        _font.Draw(sb, name, new Vector2(x + (w - nameW) / 2f, cy0), nameColor);
        cy0 += lh + 1;
        cy0 = Sepline(sb, white, x, cy0, w, pad, reqs.Count > 0);

        // Icon + requirement block.
        if (reqs.Count > 0)
        {
            var iconRect = new Rectangle(x + pad, cy0, iconBox, iconBox);
            sb.Draw(white, iconRect, new Color(0, 0, 0, 110));
            DrawBorder(sb, white, iconRect, nameColor * 0.8f);   // grade-coloured icon border
            var icon = _icons.LoadIcon(itemId);
            if (icon != null)
            {
                var dp = new Vector2(iconRect.X + (iconBox - icon.Width) / 2f, iconRect.Y + (iconBox - icon.Height) / 2f) + icon.Origin;
                icon.Draw(sb, dp);
            }
            var rx = x + pad + iconBox + iconGap;
            var ry = cy0;
            foreach (var (label, val, bad) in reqs)
            {
                _font.Draw(sb, $"{label} : ", new Vector2(rx, ry), LabelGray);
                _font.Draw(sb, val, new Vector2(rx + _font.Measure($"{label} : ").X, ry), bad ? ReqBad : ReqOk);
                ry += lh;
            }
            cy0 += blockH;
        }

        // Category / attack speed.
        cy0 = DrawSection(sb, white, x, cy0, w, pad, info, LabelGray);
        // Stat bonuses.
        cy0 = DrawSection(sb, white, x, cy0, w, pad, stats, StatColor);
        // Description.
        DrawSection(sb, white, x, cy0, w, pad, descLines, DescColor);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────
    private static bool Unmet(int have, int req) => have > 0 && have < req;

    private static int Sep(int count) => count > 0 ? 5 : 0;

    // Draws a divider line if shown; returns the y after it.
    private int Sepline(SpriteBatch sb, Texture2D white, int x, int y, int w, int pad, bool show)
    {
        if (!show) return y;
        sb.Draw(white, new Rectangle(x + pad, y + 2, w - pad * 2, 1), Divider);
        return y + 5;
    }

    private int DrawSection(SpriteBatch sb, Texture2D white, int x, int y, int w, int pad, List<string> lines, Color color)
    {
        if (lines.Count == 0) return y;
        y = Sepline(sb, white, x, y, w, pad, true);
        foreach (var s in lines)
        {
            _font.Draw(sb, s, new Vector2(x + pad, y), color);
            y += _font.LineHeight;
        }
        return y;
    }

    private static void AddStat(List<string> lines, string label, int v)
    {
        if (v != 0) lines.Add($"{label} +{v}");
    }

    private List<string> Wrap(string text, int maxW)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\\r", "\n").Replace("\\n", "\n").Split('\n'))
        {
            var words = paragraph.Split(' ');
            var cur = "";
            foreach (var word in words)
            {
                var trial = cur.Length == 0 ? word : cur + " " + word;
                if (_font.Measure(trial).X > maxW && cur.Length > 0) { lines.Add(cur); cur = word; }
                else cur = trial;
            }
            lines.Add(cur);
        }
        return lines;
    }

    private static string SpeedLabel(int s) => s switch
    {
        <= 2 => $"Faster ({s})",
        <= 4 => $"Fast ({s})",
        5    => $"Normal ({s})",
        6    => $"Slow ({s})",
        _    => $"Slower ({s})",
    };

    // reqJob bitmask: 0/-1 any; 1 Warrior, 2 Magician, 4 Bowman, 8 Thief, 16 Pirate.
    private static string JobReq(int mask)
    {
        if (mask <= 0) return "Common";
        var p = new List<string>(5);
        if ((mask & 1) != 0)  p.Add("Warrior");
        if ((mask & 2) != 0)  p.Add("Magician");
        if ((mask & 4) != 0)  p.Add("Bowman");
        if ((mask & 8) != 0)  p.Add("Thief");
        if ((mask & 16) != 0) p.Add("Pirate");
        return p.Count == 0 ? "Common" : string.Join(", ", p);
    }

    // Player job → req bit (0 = beginner/unknown). jobId%1000/100: 1 War,2 Mag,3 Bow,4 Thief,5 Pirate.
    private static int JobBit(int jobId) => (jobId % 1000) / 100 switch
    {
        1 => 1, 2 => 2, 3 => 4, 4 => 8, 5 => 16, _ => 0,
    };

    // Unmet only when the player has a definite job that the item excludes (don't red-flag a beginner).
    private static bool JobUnmet(int mask, int jobId)
    {
        if (mask <= 0) return false;
        var bit = JobBit(jobId);
        return bit != 0 && (mask & bit) == 0;
    }

    // Equip category label from the item id (cat = id/10000). Weapons get their subtype.
    private static string Category(int itemId)
    {
        var cat = itemId / 10000;
        return cat switch
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
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
