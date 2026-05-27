using MapleClaude.UI;
using Microsoft.Xna.Framework;

namespace MapleClaude.UI.Game;

/// <summary>
/// Substitution / name-lookup hooks for <see cref="ScriptText"/>. Mirrors the data the v95
/// client's <c>CTextAnalyzer</c> pulls in when it expands <c>#p #h #t #o #m #q</c> codes.
/// All lookups are optional; an unresolved code falls back to its raw token text.
/// </summary>
public sealed class ScriptSubst
{
    /// <summary>The local player's name (for <c>#h #</c>).</summary>
    public string PlayerName = string.Empty;

    /// <summary>The speaking NPC's name, used for a bare <c>#p#</c> with no id.</summary>
    public string SpeakerName = string.Empty;

    public Func<int, string?>? NpcName;
    public Func<int, string?>? ItemName;
    public Func<int, string?>? MobName;
    public Func<int, string?>? MapName;
    public Func<int, string?>? SkillName;
}

/// <summary>
/// Modern port of the v95 client's <c>CTextAnalyzer::AnalyzeText</c>: expands MapleStory
/// script format codes, resolves substitution tokens, applies colour / bold runs, marks the
/// selectable <c>#L&lt;n&gt;# … #l</c> menu links, and word-wraps the result to a content
/// width. The output is a list of positioned <see cref="Run"/>s (relative to the dialog's
/// content origin) plus the per-link hit rectangles the NPC dialog uses for menu hit-testing.
/// </summary>
public sealed class ScriptText
{
    /// <summary>A laid-out span of same-style text. Coordinates are content-relative.</summary>
    public readonly record struct Run(string Text, int X, int Y, Color Color, bool Bold, int LinkIndex, int CharStart);

    /// <summary>A clickable <c>#L#</c> menu choice and its on-screen bounds (content-relative).</summary>
    public sealed class Link
    {
        public int Index;
        public Rectangle Bounds;
    }

    public List<Run> Runs { get; } = new();
    public List<Link> Links { get; } = new();
    public int ContentHeight { get; private set; }
    public int LineHeight { get; }

    /// <summary>Total number of visible characters (for the typewriter reveal).</summary>
    public int TotalChars { get; private set; }

    // Standard MapleStory script colours.
    private static readonly Color ClrBlue   = new(0x00, 0x66, 0xFF);
    private static readonly Color ClrRed    = new(0xFF, 0x00, 0x00);
    private static readonly Color ClrGreen  = new(0x00, 0x99, 0x00);
    private static readonly Color ClrPurple = new(0x99, 0x00, 0x99);

    private readonly BuiltInFont _font;
    private readonly BuiltInFont? _bold;
    private readonly Dictionary<(int rune, bool bold), int> _widthCache = new();

    public ScriptText(string raw, int wrapWidth, int margin, BuiltInFont font, BuiltInFont? boldFont,
                      Color defaultColor, ScriptSubst subst)
    {
        _font = font;
        _bold = boldFont;
        LineHeight = font.LineHeight;

        var atoms = Tokenize(raw ?? string.Empty, defaultColor, subst);
        LayoutAtoms(atoms, wrapWidth, margin);
    }

    // ── Atom = one rune carrying its style ───────────────────────────────────────────────
    private readonly record struct Atom(int Rune, Color Color, bool Bold, int LinkIndex, bool NewLine);

    private int RuneWidth(int rune, bool bold)
    {
        if (_widthCache.TryGetValue((rune, bold), out var w)) return w;
        var s = char.ConvertFromUtf32(rune);
        var f = bold ? (_bold ?? _font) : _font;
        w = (int)f.Measure(s).X;
        _widthCache[(rune, bold)] = w;
        return w;
    }

    // ── Phase A: expand codes / substitutions into a flat atom stream ─────────────────────
    private static List<Atom> Tokenize(string raw, Color defaultColor, ScriptSubst subst)
    {
        var atoms = new List<Atom>(raw.Length);
        var color = defaultColor;
        var bold = false;
        var link = -1;

        void Emit(string text)
        {
            foreach (var rune in text.EnumerateRunes())
            {
                if (rune.Value == '\r') continue;
                if (rune.Value == '\n') { atoms.Add(new Atom(0, color, bold, link, true)); continue; }
                atoms.Add(new Atom(rune.Value, color, bold, link, false));
            }
        }

        var i = 0;
        while (i < raw.Length)
        {
            var ch = raw[i];
            if (ch != '#' || i + 1 >= raw.Length) { Emit(ch.ToString()); i++; continue; }

            var code = raw[i + 1];
            switch (code)
            {
                case 'k': color = defaultColor; i += 2; break;
                case 'b': color = ClrBlue;      i += 2; break;
                case 'r': color = ClrRed;       i += 2; break;
                case 'g': color = ClrGreen;     i += 2; break;
                case 'd': color = ClrPurple;    i += 2; break;
                case 'e': bold = true;          i += 2; break;
                case 'n': bold = false;         i += 2; break;
                case 'l': link = -1;            i += 2; break;

                case 'L':
                {
                    var j = i + 2;
                    var start = j;
                    while (j < raw.Length && char.IsDigit(raw[j])) j++;
                    int.TryParse(raw.AsSpan(start, j - start), out var idx);
                    if (j < raw.Length && raw[j] == '#') j++;   // optional closing '#'
                    link = idx;
                    i = j;
                    break;
                }

                // Substitution codes: argument runs up to the next '#'.
                case 'p': { var (id, next) = ReadArg(raw, i + 2); Emit(id is int n && subst.NpcName?.Invoke(n) is { } nm ? nm : subst.SpeakerName); i = next; break; }
                case 'h': { var (_, next)  = ReadArg(raw, i + 2); Emit(subst.PlayerName); i = next; break; }
                case 't': case 'z': { var (id, next) = ReadArg(raw, i + 2); Emit(Resolve(subst.ItemName, id) ?? RawToken(raw, i, next)); i = next; break; }
                case 'o': { var (id, next) = ReadArg(raw, i + 2); Emit(Resolve(subst.MobName,  id) ?? RawToken(raw, i, next)); i = next; break; }
                case 'm': { var (id, next) = ReadArg(raw, i + 2); Emit(Resolve(subst.MapName,  id) ?? RawToken(raw, i, next)); i = next; break; }
                case 'q': { var (id, next) = ReadArg(raw, i + 2); Emit(Resolve(subst.SkillName, id) ?? RawToken(raw, i, next)); i = next; break; }

                // Inline icon / image codes — argument-bearing, not rendered (best effort).
                case 'i': case 'v': case 's': case 'f': case 'c':
                    i = SkipArg(raw, i + 2);
                    break;

                default:
                    // Unknown code: keep the literal '#'.
                    Emit("#");
                    i++;
                    break;
            }
        }

        return atoms;
    }

    private static string? Resolve(Func<int, string?>? lookup, int? id)
        => id is int n ? lookup?.Invoke(n) : null;

    // Returns (parsed numeric arg or null, index just past the closing '#').
    private static (int? id, int next) ReadArg(string raw, int start)
    {
        var j = start;
        while (j < raw.Length && raw[j] != '#') j++;
        var arg = raw.AsSpan(start, j - start);
        var id = int.TryParse(arg, out var n) ? n : (int?)null;
        if (j < raw.Length && raw[j] == '#') j++;   // consume closing '#'
        return (id, j);
    }

    private static int SkipArg(string raw, int start)
    {
        var j = start;
        while (j < raw.Length && raw[j] != '#') j++;
        if (j < raw.Length && raw[j] == '#') j++;
        return j;
    }

    // The original "#...#" token text, used as a visible fallback when a lookup fails.
    private static string RawToken(string raw, int hashIndex, int next)
        => raw.Substring(hashIndex, Math.Min(next, raw.Length) - hashIndex);

    // ── Phase B: greedy word-wrap of the atom stream + run grouping ───────────────────────
    private void LayoutAtoms(List<Atom> atoms, int wrapWidth, int margin)
    {
        var usable = Math.Max(1, wrapWidth - 2 * margin);
        var lines = new List<List<Atom>>();
        var line = new List<Atom>();
        var lineWidth = 0;
        var lastSpace = -1;          // index in `line` of the last space (a break opportunity)
        var widthAtLastSpace = 0;

        void NewLine()
        {
            lines.Add(line);
            line = new List<Atom>();
            lineWidth = 0;
            lastSpace = -1;
            widthAtLastSpace = 0;
        }

        foreach (var a in atoms)
        {
            if (a.NewLine) { NewLine(); continue; }

            var isSpace = a.Rune == ' ';
            // Drop a space that would start a soft-wrapped line.
            if (isSpace && line.Count == 0) continue;

            var w = RuneWidth(a.Rune, a.Bold);
            if (lineWidth + w > usable && line.Count > 0)
            {
                if (lastSpace >= 0 && lastSpace < line.Count - 1)
                {
                    // Break at the last space: carry the tail word to the next line.
                    var tail = line.GetRange(lastSpace + 1, line.Count - (lastSpace + 1));
                    line.RemoveRange(lastSpace, line.Count - lastSpace);   // drop the space + tail
                    lines.Add(line);
                    line = tail;
                    lineWidth = 0;
                    foreach (var t in line) lineWidth += RuneWidth(t.Rune, t.Bold);
                    lastSpace = -1;
                    widthAtLastSpace = 0;
                }
                else
                {
                    // Long unbreakable word: hard break before this atom.
                    NewLine();
                }
            }

            if (a.Rune == ' ') { lastSpace = line.Count; widthAtLastSpace = lineWidth; }
            line.Add(a);
            lineWidth += w;
        }
        lines.Add(line);

        // Position atoms and group same-style consecutive atoms into runs.
        var charIndex = 0;
        var linkBounds = new Dictionary<int, Rectangle>();
        for (var ly = 0; ly < lines.Count; ly++)
        {
            var y = ly * LineHeight;
            var x = margin;
            var ln = lines[ly];

            var runStart = 0;
            while (runStart < ln.Count)
            {
                var a0 = ln[runStart];
                var runEnd = runStart;
                var text = new System.Text.StringBuilder();
                var runX = x;
                var runW = 0;
                while (runEnd < ln.Count
                       && ln[runEnd].Color == a0.Color
                       && ln[runEnd].Bold == a0.Bold
                       && ln[runEnd].LinkIndex == a0.LinkIndex)
                {
                    var a = ln[runEnd];
                    text.Append(char.ConvertFromUtf32(a.Rune));
                    var w = RuneWidth(a.Rune, a.Bold);
                    runW += w;
                    x += w;
                    runEnd++;
                }

                Runs.Add(new Run(text.ToString(), runX, y, a0.Color, a0.Bold, a0.LinkIndex, charIndex));
                charIndex += runEnd - runStart;

                if (a0.LinkIndex >= 0)
                {
                    var r = new Rectangle(runX, y, runW, LineHeight);
                    linkBounds[a0.LinkIndex] = linkBounds.TryGetValue(a0.LinkIndex, out var prev)
                        ? Rectangle.Union(prev, r) : r;
                }

                runStart = runEnd;
            }
        }

        TotalChars = charIndex;
        ContentHeight = lines.Count * LineHeight;
        foreach (var kv in linkBounds)
            Links.Add(new Link { Index = kv.Key, Bounds = kv.Value });
        Links.Sort((a, b) => a.Index.CompareTo(b.Index));
    }
}
