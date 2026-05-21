using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Key-binding configuration panel.
/// Displays a visual keyboard grid; clicking a slot enters rebind-mode;
/// pressing any key assigns it. Default button restores GMS v95 defaults.
///
/// KeyAction integer values match the GMS v95 server protocol exactly
/// (HeavenMS GameConstants DEFAULT_ACTION array). When the keymap packet
/// arrives from Kinoko, the server-sent action IDs wire straight in.
///
/// WZ: UIWindow.img/KeyConfig/
/// </summary>
public sealed class KeyConfig : GamePanel
{
    // ── Action enum — IDs match GMS v95 server protocol ──────────────────────
    public enum KeyAction
    {
        None              = -1,
        Equipment         = 0,
        Items             = 1,
        Stats             = 2,
        Skills            = 3,
        Friends           = 4,
        WorldMap          = 5,
        MapleChat         = 6,
        MiniMap           = 7,
        QuestLog          = 8,
        KeyBindings       = 9,
        Say               = 10,
        Whisper           = 11,
        PartyChat         = 12,
        FriendsChat       = 13,
        Menu              = 14,
        QuickSlots        = 15,
        ToggleChat        = 16,
        Guild             = 17,
        GuildChat         = 18,
        Party             = 19,
        Notifier          = 20,
        MapleNews         = 21,
        CashShop          = 22,
        AllianceChat      = 23,
        BuddyChat         = 24,   // action 24 in GMS packet
        ManageLegion      = 25,
        Medals            = 26,
        BossParty         = 27,
        // --- v95-era additions ---
        ChangeChannel     = 45,
        CharInfo          = 44,
        MainMenu          = 46,
        Screenshot        = 47,
        MapleStoage       = 200,
        Mute              = 202,
        // --- In-game actions ---
        PickUp            = 50,
        Sit               = 51,
        Attack            = 52,
        Jump              = 53,
        Interact          = 54,
        // --- Face expressions ---
        Face1             = 100,
        Face2             = 101,
        Face3             = 102,
        Face4             = 103,
        Face5             = 104,
        Face6             = 105,
        Face7             = 106,
        // --- Client-only movement (not in server protocol) ---
        MoveLeft          = 1002,
        MoveRight         = 1003,
    }

    // Label shown on each slot in the visual grid
    private static readonly Dictionary<KeyAction, string> ActionLabel = new()
    {
        [KeyAction.Equipment]    = "EQP",   [KeyAction.Items]       = "ITEM",
        [KeyAction.Stats]        = "STAT",  [KeyAction.Skills]      = "SKIL",
        [KeyAction.Friends]      = "FRND",  [KeyAction.WorldMap]    = "WMAP",
        [KeyAction.MapleChat]    = "MCHAT", [KeyAction.MiniMap]     = "MMAP",
        [KeyAction.QuestLog]     = "QLOG",  [KeyAction.KeyBindings] = "KEYS",
        [KeyAction.Say]          = "SAY",   [KeyAction.Whisper]     = "WIS",
        [KeyAction.PartyChat]    = "PCHT",  [KeyAction.FriendsChat] = "FCHT",
        [KeyAction.Menu]         = "MENU",  [KeyAction.QuickSlots]  = "QS",
        [KeyAction.ToggleChat]   = "CHAT",  [KeyAction.Guild]       = "GILD",
        [KeyAction.GuildChat]    = "GCHT",  [KeyAction.Party]       = "PTY",
        [KeyAction.Notifier]     = "NOTE",  [KeyAction.MapleNews]   = "NEWS",
        [KeyAction.CashShop]     = "CS",    [KeyAction.AllianceChat]= "ACHT",
        [KeyAction.BuddyChat]    = "BCHT",  [KeyAction.ManageLegion]= "LGN",
        [KeyAction.Medals]       = "MDL",   [KeyAction.BossParty]   = "BOSS",
        [KeyAction.ChangeChannel]= "CH",    [KeyAction.CharInfo]    = "INFO",
        [KeyAction.MainMenu]     = "HOME",  [KeyAction.Screenshot]  = "SSCR",
        [KeyAction.Mute]         = "MUTE",
        [KeyAction.PickUp]       = "PICK",  [KeyAction.Sit]         = "SIT",
        [KeyAction.Attack]       = "ATK",   [KeyAction.Jump]        = "JUMP",
        [KeyAction.Interact]     = "NACT",
        [KeyAction.Face1]        = ":-)",   [KeyAction.Face2]       = ":-D",
        [KeyAction.Face3]        = ":-O",   [KeyAction.Face4]       = ":-(",
        [KeyAction.Face5]        = ":-/",   [KeyAction.Face6]       = "B-)",
        [KeyAction.Face7]        = ":-P",
        [KeyAction.MoveLeft]     = "←",     [KeyAction.MoveRight]   = "→",
    };

    // ── Binding store ─────────────────────────────────────────────────────────
    private readonly Dictionary<Keys, KeyAction> _bindings = new();

    // ── Visual keyboard grid ──────────────────────────────────────────────────
    private static readonly (Keys key, string label)[][] KeyRows =
    [
        [ (Keys.Escape,"ESC"),
          (Keys.F1,"F1"),(Keys.F2,"F2"),(Keys.F3,"F3"),(Keys.F4,"F4"),
          (Keys.F5,"F5"),(Keys.F6,"F6"),(Keys.F7,"F7"),(Keys.F8,"F8"),
          (Keys.F9,"F9"),(Keys.F10,"F10"),(Keys.F11,"F11"),(Keys.F12,"F12"),
          (Keys.PrintScreen,"PRS"),(Keys.Insert,"INS"),(Keys.Home,"HME"),
          (Keys.PageUp,"PGU") ],
        [ (Keys.OemTilde,"`"),(Keys.D1,"1"),(Keys.D2,"2"),(Keys.D3,"3"),
          (Keys.D4,"4"),(Keys.D5,"5"),(Keys.D6,"6"),(Keys.D7,"7"),
          (Keys.D8,"8"),(Keys.D9,"9"),(Keys.D0,"0"),
          (Keys.OemMinus,"-"),(Keys.OemPlus,"=") ],
        [ (Keys.Tab,"TAB"),(Keys.Q,"Q"),(Keys.W,"W"),(Keys.E,"E"),
          (Keys.R,"R"),(Keys.T,"T"),(Keys.Y,"Y"),(Keys.U,"U"),
          (Keys.I,"I"),(Keys.O,"O"),(Keys.P,"P"),
          (Keys.OemOpenBrackets,"["),(Keys.OemCloseBrackets,"]"),
          (Keys.OemPipe,"\\") ],
        [ (Keys.CapsLock,"CAP"),(Keys.A,"A"),(Keys.S,"S"),(Keys.D,"D"),
          (Keys.F,"F"),(Keys.G,"G"),(Keys.H,"H"),(Keys.J,"J"),
          (Keys.K,"K"),(Keys.L,"L"),
          (Keys.OemSemicolon,";"),(Keys.OemQuotes,"'"),
          (Keys.Enter,"ENT") ],
        [ (Keys.LeftShift,"SHF"),(Keys.Z,"Z"),(Keys.X,"X"),(Keys.C,"C"),
          (Keys.V,"V"),(Keys.B,"B"),(Keys.N,"N"),(Keys.M,"M"),
          (Keys.OemComma,","),(Keys.OemPeriod,"."),(Keys.RightShift,"RSH"),
          (Keys.Up,"↑") ],
        [ (Keys.LeftControl,"CTR"),(Keys.LeftAlt,"ALT"),
          (Keys.Space,"SPACE"),
          (Keys.RightAlt,"RAL"),(Keys.RightControl,"RCT"),
          (Keys.Left,"←"),(Keys.Down,"↓"),(Keys.Right,"→"),
          (Keys.End,"END"),(Keys.PageDown,"PGD"),(Keys.Delete,"DEL") ],
    ];

    // ── UI ────────────────────────────────────────────────────────────────────
    private readonly WzSprite? _background;
    private readonly Button?   _btClose;
    private readonly Button?   _btDefault;
    private readonly Button?   _btOk;
    private readonly List<Button> _allButtons = new();

    private Keys?  _rebindTarget;

    private const int PanelW = 560;
    private const int PanelH = 340;
    private const int GridX  = 8;
    private const int GridY  = 28;
    private const int SlotW  = 38;
    private const int SlotH  = 26;
    private const int SlotGap = 2;

    private readonly BuiltInFont? _font;

    public KeyConfig(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(120, 130);

        var kc = ui?.GetItem("UIWindow.img/KeyConfig") as WzProperty;
        _background = kc?.Get("backgrnd") is WzCanvas bc ? loader.Load(bc) : null;

        _btClose   = MakeBtn(loader, kc, "BtClose",  () => { IsVisible = false; _rebindTarget = null; });
        _btDefault = MakeBtn(loader, kc, "BtDefault", ResetToDefault);
        _btOk      = MakeBtn(loader, kc, "BtOK",     () => { IsVisible = false; _rebindTarget = null; });

        ResetToDefault();
        LayoutButtons();
    }

    // ── Default bindings (HeavenMS GameConstants DEFAULT arrays, GMS v95) ────
    private void ResetToDefault()
    {
        _bindings.Clear();

        // ── Movement (client-only, not in server packet) ─────────────────────
        _bindings[Keys.Left]         = KeyAction.MoveLeft;
        _bindings[Keys.Right]        = KeyAction.MoveRight;
        _bindings[Keys.Up]           = KeyAction.Jump;   // up arrow also jumps

        // ── Actions (scan-code table from HeavenMS GameConstants.DEFAULT_*) ──
        // E  → Equipment(0)
        _bindings[Keys.E]            = KeyAction.Equipment;
        // I  → Items(1)
        _bindings[Keys.I]            = KeyAction.Items;
        // S  → Stats(2)
        _bindings[Keys.S]            = KeyAction.Stats;
        // K  → Skills(3)
        _bindings[Keys.K]            = KeyAction.Skills;
        // R  → Friends(4)
        _bindings[Keys.R]            = KeyAction.Friends;
        // W  → WorldMap(5)
        _bindings[Keys.W]            = KeyAction.WorldMap;
        // C  → MapleChat(6)
        _bindings[Keys.C]            = KeyAction.MapleChat;
        // M  → MiniMap(7)
        _bindings[Keys.M]            = KeyAction.MiniMap;
        // Q  → QuestLog(8)
        _bindings[Keys.Q]            = KeyAction.QuestLog;
        // \  → KeyBindings(9)
        _bindings[Keys.OemPipe]      = KeyAction.KeyBindings;
        // 1  → Say/Chat(10)
        _bindings[Keys.D1]           = KeyAction.Say;
        // H  → Whisper(11)
        _bindings[Keys.H]            = KeyAction.Whisper;
        // 2  → PartyChat(12)
        _bindings[Keys.D2]           = KeyAction.PartyChat;
        // 3  → FriendsChat(13)
        _bindings[Keys.D3]           = KeyAction.FriendsChat;
        // [  → Menu(14)
        _bindings[Keys.OemOpenBrackets]  = KeyAction.Menu;
        // ]  → QuickSlots(15)
        _bindings[Keys.OemCloseBrackets] = KeyAction.QuickSlots;
        // '  → ToggleChat(16)
        _bindings[Keys.OemQuotes]    = KeyAction.ToggleChat;
        // G  → Guild(17)
        _bindings[Keys.G]            = KeyAction.Guild;
        // 4  → GuildChat(18)
        _bindings[Keys.D4]           = KeyAction.GuildChat;
        // P  → Party(19)
        _bindings[Keys.P]            = KeyAction.Party;
        // L  → Notifier(20)
        _bindings[Keys.L]            = KeyAction.Notifier;
        // 6  → MapleNews(21)
        _bindings[Keys.D6]           = KeyAction.MapleNews;
        // B  → CashShop(22)
        _bindings[Keys.B]            = KeyAction.CashShop;
        // `  → AllianceChat(23)
        _bindings[Keys.OemTilde]     = KeyAction.AllianceChat;
        // 5  → BuddyChat(24)
        _bindings[Keys.D5]           = KeyAction.BuddyChat;
        // O  → ManageLegion(25)
        _bindings[Keys.O]            = KeyAction.ManageLegion;
        // F  → Medals(26)
        _bindings[Keys.F]            = KeyAction.Medals;
        // ;  → BossParty(27)
        _bindings[Keys.OemSemicolon] = KeyAction.BossParty;
        // Z  → PickUp(50)
        _bindings[Keys.Z]            = KeyAction.PickUp;
        // X  → Sit(51)
        _bindings[Keys.X]            = KeyAction.Sit;
        // CTRL → Attack(52)
        _bindings[Keys.LeftControl]  = KeyAction.Attack;
        _bindings[Keys.RightControl] = KeyAction.Attack;
        // ALT  → Jump(53)
        _bindings[Keys.LeftAlt]      = KeyAction.Jump;
        // SPACE → Interact/Harvest(54)
        _bindings[Keys.Space]        = KeyAction.Interact;
        // F1-F7 → Face1-7 (100-106)
        _bindings[Keys.F1]           = KeyAction.Face1;
        _bindings[Keys.F2]           = KeyAction.Face2;
        _bindings[Keys.F3]           = KeyAction.Face3;
        _bindings[Keys.F4]           = KeyAction.Face4;
        _bindings[Keys.F5]           = KeyAction.Face5;
        _bindings[Keys.F6]           = KeyAction.Face6;
        _bindings[Keys.F7]           = KeyAction.Face7;
    }

    // ── Accessors ─────────────────────────────────────────────────────────────

    public KeyAction GetAction(Keys key) =>
        _bindings.TryGetValue(key, out var a) ? a : KeyAction.None;

    public void SetBinding(Keys key, KeyAction action) => _bindings[key] = action;

    /// <summary>
    /// Returns true if ANY key bound to <paramref name="action"/> is held.
    /// Used for continuous actions polled in Update (movement, jump).
    /// </summary>
    public bool IsActionDown(KeyboardState kb, KeyAction action)
    {
        foreach (var (k, a) in _bindings)
            if (a == action && kb.IsKeyDown(k)) return true;
        return false;
    }

    public IEnumerable<Keys> KeysFor(KeyAction action) =>
        _bindings.Where(kv => kv.Value == action).Select(kv => kv.Key);

    /// <summary>
    /// Apply a full keybinding update from the server keymap packet.
    /// Called by the Kinoko packet handler once wired.
    /// type: 0=none 1=skill 2=item 3=cash 4=menu 5=action 6=face 8=macro
    /// </summary>
    public void ApplyServerKeymap(IEnumerable<(int keyIndex, int type, int actionId)> entries)
    {
        // Remove all server-driven bindings (keep MoveLeft/MoveRight which are client-only)
        var clientOnly = _bindings
            .Where(kv => kv.Value == KeyAction.MoveLeft || kv.Value == KeyAction.MoveRight)
            .ToList();
        _bindings.Clear();
        foreach (var (k, v) in clientOnly) _bindings[k] = v;

        foreach (var (keyIndex, type, actionId) in entries)
        {
            var key = ScanCodeToKeys(keyIndex);
            if (key == null || type == 0) continue;
            var action = actionId switch
            {
                var id when Enum.IsDefined(typeof(KeyAction), id) => (KeyAction)id,
                _ => KeyAction.None,
            };
            if (action != KeyAction.None) _bindings[key.Value] = action;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt) => LayoutButtons();

    // ── Draw ──────────────────────────────────────────────────────────────────

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;

        var px = (int)Position.X;
        var py = (int)Position.Y;

        if (_background != null)
            _background.Draw(sb, Position + new Vector2(PanelW / 2f, PanelH / 2f));
        else
        {
            sb.Draw(white, new Rectangle(px, py, PanelW, PanelH), new Color(12, 12, 22, 240));
            DrawBorder(sb, white, new Rectangle(px, py, PanelW, PanelH), new Color(60, 60, 80));
        }

        _font?.Draw(sb, "Key Configuration", new Vector2(px + 200, py + 6),
            new Color(220, 200, 150));

        if (_rebindTarget.HasValue)
        {
            var msg = $"Press a key to bind to: {SlotLabel(_rebindTarget.Value)}  (ESC = cancel)";
            if (_font != null)
            {
                var sz = _font.Measure(msg);
                _font.Draw(sb, msg,
                    new Vector2(px + (PanelW - (int)sz.X) / 2, py + PanelH - 18),
                    new Color(255, 220, 60));
            }
        }

        DrawKeyGrid(sb, white, px, py);
        foreach (var b in _allButtons) b.Draw(sb);
    }

    private void DrawKeyGrid(SpriteBatch sb, Texture2D white, int px, int py)
    {
        var rowY = py + GridY;
        foreach (var row in KeyRows)
        {
            var slotX = px + GridX;
            foreach (var (key, keyLabel) in row)
            {
                var w = keyLabel.Length > 2 ? SlotW + 10 : SlotW;
                DrawKeySlot(sb, white, slotX, rowY, w, key, keyLabel);
                slotX += w + SlotGap;
            }
            rowY += SlotH + SlotGap;
        }
    }

    private void DrawKeySlot(SpriteBatch sb, Texture2D white,
        int x, int y, int w, Keys key, string keyLabel)
    {
        var isTarget = _rebindTarget == key;
        _bindings.TryGetValue(key, out var action);
        var hasBind  = action != KeyAction.None && action != default;

        Color fill   = isTarget ? new Color(80, 60, 20)
                     : hasBind  ? new Color(30, 40, 55)
                     : new Color(22, 22, 32);
        Color border = isTarget ? new Color(240, 180, 40)
                     : hasBind  ? new Color(60, 90, 130)
                     : new Color(55, 55, 75);

        sb.Draw(white, new Rectangle(x, y, w, SlotH), fill);
        DrawBorder(sb, white, new Rectangle(x, y, w, SlotH), border);

        // Key name (top-left, dim)
        _font?.Draw(sb, keyLabel, new Vector2(x + 2, y + 1), new Color(130, 135, 170));

        // Action label (bottom)
        if (hasBind && ActionLabel.TryGetValue(action, out var al))
            _font?.Draw(sb, al, new Vector2(x + 2, y + 13), new Color(200, 220, 200));
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;

        foreach (var b in _allButtons)
            if (b.HandleMouseButton(x, y, down)) return true;

        if (!down) return false;

        var px = (int)Position.X;
        var py = (int)Position.Y;
        var rowY = py + GridY;
        foreach (var row in KeyRows)
        {
            var slotX = px + GridX;
            foreach (var (key, keyLabel) in row)
            {
                var w = keyLabel.Length > 2 ? SlotW + 10 : SlotW;
                if (new Rectangle(slotX, rowY, w, SlotH).Contains(x, y))
                {
                    _rebindTarget = key;
                    return true;
                }
                slotX += w + SlotGap;
            }
            rowY += SlotH + SlotGap;
        }

        return new Rectangle(px, py, PanelW, PanelH).Contains(x, y);
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;

        if (_rebindTarget.HasValue)
        {
            if (key == Keys.Escape) { _rebindTarget = null; return true; }
            var oldAction = _bindings.TryGetValue(_rebindTarget.Value, out var a) ? a : KeyAction.None;
            // Remove any existing binding for this physical key
            _bindings.Remove(key);
            // Re-assign the action from the old slot to the new key
            if (oldAction != KeyAction.None) _bindings[key] = oldAction;
            _bindings.Remove(_rebindTarget.Value);
            _rebindTarget = key;
            return true;
        }

        if (key == Keys.Escape) { IsVisible = false; return true; }
        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string SlotLabel(Keys k)
    {
        foreach (var row in KeyRows)
            foreach (var (key, label) in row)
                if (key == k) return label;
        return k.ToString();
    }

    private void LayoutButtons()
    {
        if (_btClose   != null) _btClose.Position   = Position + new Vector2(PanelW - 20, 4);
        if (_btDefault != null) _btDefault.Position = Position + new Vector2(PanelW - 150, PanelH - 22);
        if (_btOk      != null) _btOk.Position      = Position + new Vector2(PanelW - 60,  PanelH - 22);
    }

    private Button? MakeBtn(WzTextureLoader loader, WzProperty? root, string name, Action onClick)
    {
        var pr = root?.Get(name) as WzProperty;
        if (pr is null) return null;
        var b = new Button(loader, pr) { OnClick = onClick };
        _allButtons.Add(b);
        return b;
    }

    // Map HeavenMS scan-code indices (from DEFAULT_KEY array) to MonoGame Keys
    private static Keys? ScanCodeToKeys(int sc) => sc switch
    {
        2  => Keys.D1,      3  => Keys.D2,      4  => Keys.D3,      5  => Keys.D4,
        6  => Keys.D5,      7  => Keys.D6,       8  => Keys.D7,      9  => Keys.D8,
        10 => Keys.D9,      11 => Keys.D0,
        12 => Keys.OemMinus, 13 => Keys.OemPlus,
        16 => Keys.Q,       17 => Keys.W,        18 => Keys.E,       19 => Keys.R,
        20 => Keys.T,       21 => Keys.Y,        22 => Keys.U,       23 => Keys.I,
        24 => Keys.O,       25 => Keys.P,
        26 => Keys.OemOpenBrackets, 27 => Keys.OemCloseBrackets,
        29 => Keys.LeftControl,
        30 => Keys.A,       31 => Keys.S,        32 => Keys.D,       33 => Keys.F,
        34 => Keys.G,       35 => Keys.H,        36 => Keys.J,       37 => Keys.K,
        38 => Keys.L,
        39 => Keys.OemSemicolon,
        40 => Keys.OemQuotes,
        41 => Keys.OemTilde,
        42 => Keys.LeftShift,
        43 => Keys.OemPipe,
        44 => Keys.Z,       45 => Keys.X,        46 => Keys.C,       47 => Keys.V,
        48 => Keys.B,       49 => Keys.N,        50 => Keys.M,
        51 => Keys.OemComma, 52 => Keys.OemPeriod,
        56 => Keys.LeftAlt, 57 => Keys.Space,
        59 => Keys.F1,      60 => Keys.F2,       61 => Keys.F3,      62 => Keys.F4,
        63 => Keys.F5,      64 => Keys.F6,       65 => Keys.F7,      66 => Keys.F8,
        67 => Keys.F9,      68 => Keys.F10,      69 => Keys.F11,     70 => Keys.F12,
        71 => Keys.Home,    73 => Keys.PageUp,    79 => Keys.End,     81 => Keys.PageDown,
        82 => Keys.Insert,  83 => Keys.Delete,   84 => Keys.Escape,
        85 => Keys.RightControl, 86 => Keys.RightShift, 87 => Keys.RightAlt,
        88 => Keys.Scroll,
        _ => null,
    };

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
