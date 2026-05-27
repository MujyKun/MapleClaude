using MapleClaude.Character;
using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Authentic NPC script / quest dialog — a port of the v95 client's <c>CUtilDlgEx</c>
/// (driven by <c>CScriptMan</c>). The frame, name bar and buttons come from
/// <c>UI.wz/UIWindow2.img/UtilDlgEx</c> (<c>t</c>/<c>c</c>/<c>s</c> vertical 3-slice +
/// <c>bar</c> + <c>Bt*</c>); the speaker plays its animated <c>stand</c> action from
/// <c>Npc.wz</c>; the body text is laid out by <see cref="ScriptText"/> with the same
/// geometry constants the original client uses, a typewriter reveal, and inline
/// <c>#L#…#l</c> menu links.
/// </summary>
public sealed class NpcTalk : GamePanel
{
    public enum DlgType { Say, YesNo, Menu, AskText, AskNumber, AskBoxText, SayImage }

    // ── ScriptMessageParam flags (mirror MapleClaude.Net.Packet.ScriptMessageParam) ──────
    private const byte ParamNotCancellable = 0x01;
    private const byte ParamPlayerSpeaker  = 0x02;
    private const byte ParamSpeakerRight   = 0x04;
    private const byte ParamFlipSpeaker    = 0x08;

    // Authentic dark-grey body text colour; name bar text is white.
    private static readonly Color BodyColor = new(0x55, 0x55, 0x55);
    private const float TypeCharsPerSec = 45f;

    private readonly WzTextureLoader _loader;
    private readonly WzPackage? _npcWz;
    private readonly BuiltInFont _body;
    private readonly BuiltInFont _bold;
    private readonly BuiltInFont _name;
    private readonly ScriptSubst _subst;

    // Frame + name bar
    private readonly WzSprite? _top;
    private readonly WzSprite? _fill;
    private readonly WzSprite? _bottom;
    private readonly WzSprite? _bar;

    // Buttons
    private readonly Button? _btOk, _btYes, _btNo, _btNext, _btPrev, _btClose, _btQYes, _btQNo;

    private readonly TextField _input = new() { DrawFallbackBox = false, TextColor = Color.Black };

    // ── Callbacks (set per message by GameStage) ─────────────────────────────────────────
    public Action? OnOk { get; set; }
    public Action? OnYes { get; set; }
    public Action? OnNo { get; set; }
    public Action? OnNext { get; set; }
    public Action? OnPrev { get; set; }
    public Action? OnClose { get; set; }
    public Action<int>? OnMenuChoice { get; set; }
    public Action<string>? OnTextConfirm { get; set; }
    public Action<int>? OnNumberConfirm { get; set; }

    // ── Per-message state ────────────────────────────────────────────────────────────────
    private DlgType _type = DlgType.Say;
    private byte _param;
    private bool _quest;
    private bool _hasPrev, _hasNext;
    private int _minNum, _maxNum;
    private NpcLook? _speaker;
    private int _speakerId = -1;
    private string _speakerName = string.Empty;
    private ScriptText? _script;

    // Computed layout (window-relative)
    private int _wndWidth, _wndHeight, _ctLeft, _ctTop, _scrHeight, _textH;
    private bool _scrollBar;
    private int _scrollPos;

    private float _reveal;            // typewriter char count
    private int _menuHover = -1;
    private int _viewW = 1024, _viewH = 768;

    private readonly List<(Button btn, Vector2 off, Action act)> _active = new();

    public NpcTalk(WzTextureLoader loader, WzPackage? ui, WzPackage? npcWz,
                   BuiltInFont body, BuiltInFont bold, BuiltInFont name, ScriptSubst subst)
    {
        _loader = loader;
        _npcWz = npcWz;
        _body = body;
        _bold = bold;
        _name = name;
        _subst = subst;
        _input.Font = body;
        IsVisible = false;

        var dlg = ui?.GetItem("UIWindow2.img/UtilDlgEx") as WzProperty;
        _top    = dlg?.Get("t") is WzCanvas tc ? loader.Load(tc) : null;
        _fill   = dlg?.Get("c") is WzCanvas cc ? loader.Load(cc) : null;
        _bottom = dlg?.Get("s") is WzCanvas sc ? loader.Load(sc) : null;
        _bar    = dlg?.Get("bar") is WzCanvas bc ? loader.Load(bc) : null;

        _btOk    = MakeButton(dlg, "BtOK",   "OK");
        _btYes   = MakeButton(dlg, "BtYes",  "Yes");
        _btNo    = MakeButton(dlg, "BtNo",   "No");
        _btNext  = MakeButton(dlg, "BtNext", "Next");
        _btPrev  = MakeButton(dlg, "BtPrev", "Prev");
        _btClose = MakeButton(dlg, "BtClose", "X");
        _btQYes  = MakeButton(dlg, "BtQYes", "Accept");
        _btQNo   = MakeButton(dlg, "BtQNo",  "Decline");
    }

    private Button? MakeButton(WzProperty? dlg, string node, string fallback)
    {
        var b = new Button(_loader, dlg?.Get(node) as WzProperty)
        {
            FallbackLabel = fallback,
            Font = _name,
        };
        return b;
    }

    // ── Show entry points ────────────────────────────────────────────────────────────────

    public void ShowSay(int speakerId, byte param, string text, bool hasPrev, bool hasNext)
    {
        Begin(DlgType.Say, speakerId, param);
        _hasPrev = hasPrev;
        _hasNext = hasNext;
        BuildScript(text);
        Finish();
    }

    public void ShowYesNo(int speakerId, byte param, string text, bool quest)
    {
        Begin(DlgType.YesNo, speakerId, param);
        _quest = quest;
        BuildScript(text);
        Finish();
    }

    public void ShowMenu(int speakerId, byte param, string text)
    {
        Begin(DlgType.Menu, speakerId, param);
        BuildScript(text);
        _reveal = _script?.TotalChars ?? 0;   // menu items show immediately so they're readable/clickable
        Finish();
    }

    public void ShowAskText(int speakerId, byte param, string text, string def, int minLen, int maxLen, bool boxText = false)
    {
        Begin(boxText ? DlgType.AskBoxText : DlgType.AskText, speakerId, param);
        BuildScript(text);
        _reveal = _script?.TotalChars ?? 0;   // menus / inputs show their text immediately
        _input.IsPassword = false;
        _input.Text = def ?? string.Empty;
        _input.MaxLength = Math.Max(1, maxLen > 0 ? maxLen : 24);
        _input.IsFocused = true;
        Finish();
    }

    public void ShowAskNumber(int speakerId, byte param, string text, int def, int min, int max)
    {
        Begin(DlgType.AskNumber, speakerId, param);
        BuildScript(text);
        _reveal = _script?.TotalChars ?? 0;   // menus / inputs show their text immediately
        _minNum = min;
        _maxNum = max <= 0 ? int.MaxValue : max;
        _input.IsPassword = false;
        _input.Text = def.ToString();
        _input.MaxLength = 10;
        _input.IsFocused = true;
        Finish();
    }

    private void Begin(DlgType type, int speakerId, byte param)
    {
        _type = type;
        _param = param;
        _quest = false;
        _hasPrev = _hasNext = false;
        _scrollPos = 0;
        _reveal = 0;
        _menuHover = -1;

        // Reuse the speaker across paging (Say next/prev) when the NPC is unchanged.
        if (speakerId != _speakerId || _speaker is null)
        {
            _speakerId = speakerId;
            if ((param & ParamPlayerSpeaker) != 0 || speakerId <= 0)
            {
                _speaker = null;
                _speakerName = _subst.PlayerName;
            }
            else
            {
                _speaker = new NpcLook(speakerId, Vector2.Zero, _name);
                _speaker.Load(_loader, _npcWz);
                _speaker.SetState("stand");
                _speakerName = _speaker.Name;
            }
        }
        _subst.SpeakerName = _speakerName;
    }

    private void BuildScript(string text)
    {
        var (wrap, margin) = ContentMetrics();
        _script = new ScriptText(text, wrap, margin, _body, _bold, BodyColor, _subst);
        _textH = _script.ContentHeight;
    }

    private void Finish()
    {
        ComputeLayout();
        IsVisible = true;
    }

    // ── Authentic geometry (GetWndWidth / GetBasicCTWidth / GetBasicCTMargin / clamps) ────

    private bool NoNpc => _speaker is null && (_param & ParamPlayerSpeaker) == 0;

    private int WndWidth => _type == DlgType.SayImage ? 418 : (NoNpc ? 260 : 519);

    private (int wrap, int margin) ContentMetrics() => _type switch
    {
        DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText => (NoNpc ? 236 : 340, 2),
        DlgType.SayImage => (400, 8),
        _ => (NoNpc ? 210 : 341, 8),   // Say / YesNo / Menu
    };

    private int CtHeightMax => _type switch
    {
        DlgType.Say or DlgType.YesNo or DlgType.Menu => 240,
        DlgType.SayImage => 300,
        _ => int.MaxValue,   // input types are unbounded
    };

    private int CtHeightMin => _type == DlgType.SayImage ? 300 : (NoNpc ? 0 : 110);

    private void ComputeLayout()
    {
        _wndWidth = WndWidth;

        // Content height = analysed text height, plus the menu trailing gap / input box room.
        var contentH = _textH;
        if (_type == DlgType.Menu) contentH += 5;
        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText) contentH += 24;

        var max = CtHeightMax;
        var min = CtHeightMin;
        _scrollBar = contentH > max;
        _scrHeight = Math.Clamp(contentH, min, max);

        var speakerRight = (_param & ParamSpeakerRight) != 0;
        _ctLeft = (speakerRight ? 23 : 158) - (_scrollBar ? 4 : 0);
        _ctTop  = (contentH <= _scrHeight ? (_scrHeight - contentH + 6) / 2 : -6) + 22;
        _wndHeight = _scrHeight + 80;

        // Centre on screen (CreateUtilDlgEx → CreateDlg centred).
        Position = new Vector2((_viewW - _wndWidth) / 2f, (_viewH - _wndHeight) / 2f);

        BuildButtons();
        ApplyButtonPositions();
    }

    public override void Relayout(int viewWidth, int viewHeight)
    {
        _viewW = viewWidth;
        _viewH = viewHeight;
        if (IsVisible) ComputeLayout();
    }

    // ── Buttons (OnCreate_TEXT / _YESNO / _LIST / _INPUT positions) ───────────────────────

    private void BuildButtons()
    {
        _active.Clear();
        var w = _wndWidth;
        var h = _wndHeight;
        var right = (_param & ParamSpeakerRight) != 0 ? 140 : 10;
        var cancellable = (_param & ParamNotCancellable) == 0;

        switch (_type)
        {
            case DlgType.Say:
            case DlgType.SayImage:
                if (_hasPrev) Add(_btPrev, w - right - 106 - (_scrollBar ? 5 : 0), h - 57, HandlePrev);
                if (_hasNext) Add(_btNext, w - right - 58  - (_scrollBar ? 5 : 0), h - 57, HandleNext);
                else          Add(_btOk,   w - 48, h - 24, HandleOk);
                break;

            case DlgType.YesNo:
                if (_quest)
                {
                    Add(_btQYes, w - 130, h - 24, HandleYes);
                    Add(_btQNo,  w - 65,  h - 24, HandleNo);
                }
                else
                {
                    Add(_btYes, w - 130, h - 24, HandleYes);
                    Add(_btNo,  w - 65,  h - 24, HandleNo);
                }
                break;

            case DlgType.Menu:
                // Selection is by clicking a link; a Next button confirms the focused item.
                break;

            case DlgType.AskText:
            case DlgType.AskNumber:
            case DlgType.AskBoxText:
                Add(_btOk, w - 48, h - 24, HandleOk);
                break;
        }

        if (cancellable)
            Add(_btClose, _type == DlgType.Menu ? 10 : 9, h - 24, HandleClose);
    }

    private void Add(Button? b, int x, int y, Action act)
    {
        if (b is null) return;
        _active.Add((b, new Vector2(x, y), act));
    }

    private void ApplyButtonPositions()
    {
        foreach (var (b, off, _) in _active)
            b.Position = Position + off;

        // Input box position (OnCreate_INPUT): below the prompt text.
        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText)
        {
            _input.Position = new Vector2(Position.X + _ctLeft + 2, Position.Y + _ctTop + _textH + 5);
            _input.Width = _wndWidth - (2 * _ctLeft + 2);
            _input.Height = 14;
        }
    }

    // ── Update ───────────────────────────────────────────────────────────────────────────

    public override void Update(GameTime gameTime)
    {
        if (!IsVisible)
        {
            if (_input.IsFocused) _input.IsFocused = false;
            return;
        }
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _speaker?.Update(dt);
        _input.Update(gameTime);
        if (_script != null && _reveal < _script.TotalChars)
            _reveal += dt * TypeCharsPerSec;
        ApplyButtonPositions();

        if (_type == DlgType.Menu)
        {
            var ms = Mouse.GetState();
            UpdateMenuHover(ms.X, ms.Y);
        }
    }

    // ── Draw ─────────────────────────────────────────────────────────────────────────────

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible || _script is null) return;

        DrawFrame(sb, white);
        if (!NoNpc) DrawSpeaker(sb);
        DrawBody(sb, white);

        if (_type == DlgType.Menu) DrawMenuUnderline(sb, white);
        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText) DrawInput(sb, white);

        foreach (var (b, _, _) in _active) { b.WhitePixel ??= white; b.Draw(sb); }
    }

    private void DrawFrame(SpriteBatch sb, Texture2D white)
    {
        if (_top is null || _fill is null || _bottom is null)
        {
            // Asset fallback: solid panel so the dialog is still usable.
            var r = new Rectangle((int)Position.X, (int)Position.Y, _wndWidth, _wndHeight);
            sb.Draw(white, r, new Color(245, 235, 210));
            sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), new Color(120, 100, 70));
            sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), new Color(120, 100, 70));
            sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), new Color(120, 100, 70));
            sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), new Color(120, 100, 70));
            return;
        }

        var x = (int)Position.X;
        var topH = _top.Height;
        var botH = _bottom.Height;
        var span = Math.Max(0, _wndHeight - topH - botH);
        _top.Draw(sb, Position);
        sb.Draw(_fill.Texture, new Rectangle(x, (int)Position.Y + topH, _fill.Width, span), Color.White);
        _bottom.Draw(sb, new Vector2(x, Position.Y + _wndHeight - botH));
    }

    private void DrawSpeaker(SpriteBatch sb)
    {
        var topH = _top?.Height ?? 0;
        var botH = _bottom?.Height ?? 0;
        var barW = _bar?.Width ?? 121;
        var barH = _bar?.Height ?? 23;
        var speakerY = (topH + (_wndHeight - topH - botH) + botH) / 2;
        var speakerRight = (_param & ParamSpeakerRight) != 0;
        var regionX = speakerRight ? _wndWidth - 22 - barW : 22;
        var speakerPos = Position + new Vector2(regionX, 11 + speakerY);
        var center = speakerPos + new Vector2(barW / 2f, 0);

        var flip = (_param & ParamFlipSpeaker) != 0;
        _speaker?.DrawFrameOnly(sb, center, flip);
        _bar?.Draw(sb, speakerPos);

        if (!string.IsNullOrEmpty(_speakerName))
        {
            var sz = _name.Measure(_speakerName);
            var ny = speakerPos.Y + (barH - _name.LineHeight) / 2f;
            _name.Draw(sb, _speakerName, new Vector2(center.X - sz.X / 2f, ny), Color.White);
        }
    }

    private void DrawBody(SpriteBatch sb, Texture2D white)
    {
        if (_script is null) return;
        var origin = new Vector2(Position.X + _ctLeft, Position.Y + _ctTop - _scrollPos);
        var reveal = (int)_reveal;
        foreach (var run in _script.Runs)
        {
            var yLocal = run.Y - _scrollPos;
            if (yLocal + _script.LineHeight < 0 || yLocal > _scrHeight + 4) continue; // cull to content
            if (run.CharStart >= reveal) continue;

            var text = run.CharStart + run.Text.Length <= reveal
                ? run.Text
                : run.Text[..Math.Max(0, reveal - run.CharStart)];
            if (text.Length == 0) continue;

            var font = run.Bold ? _bold : _body;
            font.Draw(sb, text, new Vector2(origin.X + run.X, origin.Y + run.Y), run.Color);
        }
    }

    private void DrawMenuUnderline(SpriteBatch sb, Texture2D white)
    {
        if (_script is null || _menuHover < 0) return;
        foreach (var link in _script.Links)
        {
            if (link.Index != _menuHover) continue;
            var b = link.Bounds;
            var y = (int)(Position.Y + _ctTop - _scrollPos) + b.Bottom - 1;
            // Dotted gray underline (CUtilDlgEx draws 0xFF919191 segments).
            for (var dx = 0; dx < b.Width; dx += 3)
                sb.Draw(white, new Rectangle((int)(Position.X + _ctLeft) + b.X + dx, y, 2, 1), new Color(0x91, 0x91, 0x91));
        }
    }

    private void DrawInput(SpriteBatch sb, Texture2D white)
    {
        // White recess with a 1px black border, just under the prompt (OnCreate_INPUT).
        var bx = (int)Position.X + _ctLeft - 1;
        var by = (int)Position.Y + _ctTop + _textH + 2;
        var bw = _wndWidth - 2 * _ctLeft + 3;
        sb.Draw(white, new Rectangle(bx, by, bw, 18), Color.Black);
        sb.Draw(white, new Rectangle(bx + 1, by + 1, bw - 2, 16), Color.White);
        _input.Draw(sb, white);
    }

    // ── Button handlers ──────────────────────────────────────────────────────────────────

    private void HandleOk()
    {
        switch (_type)
        {
            case DlgType.AskText:
            case DlgType.AskBoxText:
                var t = _input.Text;
                Hide();
                OnTextConfirm?.Invoke(t);
                break;
            case DlgType.AskNumber:
                var raw = _input.Text;
                Hide();
                if (int.TryParse(raw, out var n))
                    OnNumberConfirm?.Invoke(Math.Clamp(n, _minNum, _maxNum));
                else
                    OnNumberConfirm?.Invoke(Math.Clamp(0, _minNum, _maxNum));
                break;
            default:
                Hide();
                OnOk?.Invoke();
                break;
        }
    }

    private void HandleYes()  { Hide(); OnYes?.Invoke(); }
    private void HandleNo()   { Hide(); OnNo?.Invoke(); }
    private void HandleNext() { OnNext?.Invoke(); }      // stays open; server sends the next page
    private void HandlePrev() { OnPrev?.Invoke(); }
    private void HandleClose(){ Hide(); OnClose?.Invoke(); }

    private void Hide()
    {
        IsVisible = false;
        _input.IsFocused = false;
        _input.Clear();
    }

    // ── Input routing ────────────────────────────────────────────────────────────────────

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;

        // Buttons first.
        foreach (var (b, _, act) in _active)
        {
            if (b.HandleMouseButton(x, y, down))
            {
                if (!down) act();
                return true;
            }
        }

        // Menu link selection.
        if (_type == DlgType.Menu && _script != null)
        {
            var ox = (int)(Position.X + _ctLeft);
            var oy = (int)(Position.Y + _ctTop - _scrollPos);
            _menuHover = -1;
            foreach (var link in _script.Links)
            {
                var r = new Rectangle(ox + link.Bounds.X, oy + link.Bounds.Y, link.Bounds.Width, link.Bounds.Height);
                if (r.Contains(x, y))
                {
                    if (down) _menuHover = link.Index;
                    else { Hide(); OnMenuChoice?.Invoke(link.Index); }
                    return true;
                }
            }
        }

        // Input focus.
        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText)
            _input.HandleMouseButton(x, y, down);

        // Click on the body completes the typewriter reveal.
        if (down && _script != null && _reveal < _script.TotalChars)
            _reveal = _script.TotalChars;

        // Swallow clicks anywhere on the dialog.
        return new Rectangle((int)Position.X, (int)Position.Y, _wndWidth, _wndHeight).Contains(x, y);
    }

    private void UpdateMenuHover(int x, int y)
    {
        if (_script is null) return;
        var ox = (int)(Position.X + _ctLeft);
        var oy = (int)(Position.Y + _ctTop - _scrollPos);
        _menuHover = -1;
        foreach (var link in _script.Links)
        {
            var r = new Rectangle(ox + link.Bounds.X, oy + link.Bounds.Y, link.Bounds.Width, link.Bounds.Height);
            if (r.Contains(x, y)) { _menuHover = link.Index; break; }
        }
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;

        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText)
        {
            var kb = Keyboard.GetState();
            if (_input.OnKeyPress(key, kb)) return true;
            if (key == Keys.Enter)  { HandleOk(); return true; }
            if (key == Keys.Escape) { HandleClose(); return true; }
            return true;
        }

        if (key == Keys.Enter)
        {
            // Advance: Next if present, else Yes/OK.
            if (_type == DlgType.YesNo) HandleYes();
            else if (_hasNext) HandleNext();
            else HandleOk();
            return true;
        }
        if (key == Keys.Escape) { HandleClose(); return true; }
        return true;
    }

    public override void OnTextInput(char character)
    {
        if (!IsVisible) return;
        if (_type is DlgType.AskText or DlgType.AskNumber or DlgType.AskBoxText)
        {
            if (_type == DlgType.AskNumber && !char.IsDigit(character) && character != '\b' && character != '-') return;
            _input.OnTextInput(character);
        }
    }
}
