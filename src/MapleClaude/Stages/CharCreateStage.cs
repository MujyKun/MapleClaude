using MapleClaude.App;
using MapleClaude.Character;
using MapleClaude.Domain;
using MapleClaude.Net.Handlers;
using MapleClaude.Net.Packet;
using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Stages;

/// <summary>
/// Character creation flow. Per the v95 GMS asset set, presents:
///   1. A race-select panel (Normal, Cygnus/Knight, Aran, Evan, Resistance, Dual).
///   2. A name field + valid look picker (face, hair, skin, top, pants, shoes, weapon).
///   3. A confirm dialog that sends <c>CreateNewCharacter(22)</c>.
///
/// The Kinoko server enum <c>RaceSelect</c> exposes 5 races (Resistance=0,
/// Normal=1, Cygnus=2, Aran=3, Evan=4); Dual Blade is sent as Normal with a
/// non-zero <c>subJob</c> — but only the legacy "Cygnus / Aran / Evan /
/// Normal / Resistance" buttons are accepted at character creation by
/// upstream Kinoko. We still render all 6 race buttons; clicking Dual sets
/// race=1 (Normal) + subJob=1 to indicate the rogue → dual blade upgrade
/// path on first job advance.
/// </summary>
public sealed class CharCreateStage : Stage
{
    private enum Phase { RaceSelect, Customize, Confirm }

    private readonly ILogger<CharCreateStage> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly WzPackage? _ui;
    private readonly WzPackage? _map;
    private readonly WzPackage? _sound;
    private readonly List<CharacterEntry> _existingCharacters;

    private WzTextureLoader? _loader;
    private CharacterRenderer? _charRenderer;
    private Phase _phase = Phase.RaceSelect;
    private int _selectedRace = 1;             // 0 Resistance, 1 Normal, 2 Cygnus, 3 Aran, 4 Evan, 5 Dual
    private byte _selectedGender = 0;          // 0=male, 1=female
    private string _name = string.Empty;
    private string _statusLabel = string.Empty;
    private TextField? _nameField;
    private readonly List<Button> _raceButtons = new();
    private readonly List<Button> _confirmButtons = new();
    private readonly List<Button> _customizeButtons = new();
    private WzSprite? _bg;
    private WzSprite? _stepIndicator;

    private int _faceIndex;
    private int _hairIndex;
    private int _hairColorIndex;
    private int _skinIndex;
    private int _coatIndex;
    private int _pantsIndex;
    private int _shoesIndex;
    private int _weaponIndex;

    // Default starting items per gender; these match upstream Kinoko's
    // Etc/MakeCharInfo lookups for a Normal explorer.
    private static readonly int[] MaleFaces = { 20000, 20001, 20002 };
    private static readonly int[] FemaleFaces = { 21000, 21001, 21002 };
    private static readonly int[] MaleHairs = { 30000, 30020, 30030 };
    private static readonly int[] FemaleHairs = { 31000, 31040, 31050 };
    private static readonly int[] HairColors = { 0, 1, 2, 3, 4, 5, 6, 7 };
    private static readonly int[] Skins = { 0, 1, 2, 3, 9, 10 };
    private static readonly int[] MaleCoats = { 1040002, 1040006, 1040010 };
    private static readonly int[] FemaleCoats = { 1041002, 1041006, 1041010 };
    private static readonly int[] MalePants = { 1060002, 1060006 };
    private static readonly int[] FemalePants = { 1061002, 1061008 };
    private static readonly int[] Shoes = { 1072001, 1072005, 1072037, 1072038 };
    private static readonly int[] Weapons = { 1302000, 1322005, 1442079 };

    public CharCreateStage(
        ILogger<CharCreateStage> logger,
        ILoggerFactory loggerFactory,
        WzPackage? ui,
        WzPackage? map,
        WzPackage? sound,
        List<CharacterEntry> existing)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _ui = ui;
        _map = map;
        _sound = sound;
        _existingCharacters = existing;
    }

    public override void OnEnter(MapleClaudeGame game)
    {
        base.OnEnter(game);
        _loader = new WzTextureLoader(GraphicsDevice);
        _charRenderer = new CharacterRenderer(
            _loggerFactory.CreateLogger<CharacterRenderer>(),
            game.CharacterWz, game.ItemWz, _loader);
        _bg = LoadCanvas("Login.img/RaceSelect/backgrnd");
        _stepIndicator = LoadCanvas("Login.img/Common/step/3");

        BuildRaceButtons();
        BuildCustomizeButtons();

        Game.LoginHandlers.OnCheckDuplicatedIdResult += OnCheckDuplicatedId;
        Game.LoginHandlers.OnCreateCharacterResult += OnCreateCharacterResult;
    }

    public override void OnExit()
    {
        Game.LoginHandlers.OnCheckDuplicatedIdResult -= OnCheckDuplicatedId;
        Game.LoginHandlers.OnCreateCharacterResult -= OnCreateCharacterResult;
        _loader?.Dispose();
        _loader = null;
        base.OnExit();
    }

    public override void Update(GameTime gameTime)
    {
        _nameField?.Update(gameTime);
    }

    public override void Draw(GameTime gameTime, SpriteBatch sb)
    {
        var pp = GraphicsDevice.PresentationParameters;
        var w = pp.BackBufferWidth;
        var h = pp.BackBufferHeight;
        sb.Draw(Game.WhitePixel, pp.Bounds, new Color(15, 20, 40));
        _bg?.Draw(sb, new Vector2(w / 2f, h / 2f));
        _stepIndicator?.Draw(sb, new Vector2(72, 50));

        switch (_phase)
        {
            case Phase.RaceSelect:
                DrawRaceSelect(sb, w, h);
                break;
            case Phase.Customize:
                DrawCustomize(sb, w, h);
                break;
            case Phase.Confirm:
                DrawConfirm(sb, w, h);
                break;
        }

        if (!string.IsNullOrEmpty(_statusLabel) && Game.Font is not null)
        {
            var size = Game.Font.Measure(_statusLabel);
            Game.Font.Draw(sb, _statusLabel, new Vector2(w / 2f - size.X / 2f, h - 30), Color.White);
        }
    }

    private void DrawRaceSelect(SpriteBatch sb, int w, int h)
    {
        _ = w; _ = h;
        if (Game.Font is not null)
        {
            Game.Font.Draw(sb, "Pick a race", new Vector2(40, 120), Color.White);
        }
        foreach (var b in _raceButtons)
        {
            b.Draw(sb);
        }
    }

    private void DrawCustomize(SpriteBatch sb, int w, int h)
    {
        var font = Game.Font;
        if (font is not null)
        {
            font.Draw(sb, $"Name: {_name}_", new Vector2(40, 80), Color.White);
        }
        // Mini avatar preview, with the currently selected look.
        var look = BuildPreviewLook();
        _charRenderer?.Draw(sb, look, stat: null, Stance.Stand1, frame: 0,
            footPos: new Vector2(w / 2f, h / 2f), facingLeft: false);
        foreach (var b in _customizeButtons)
        {
            b.Draw(sb);
        }

        if (font is not null)
        {
            font.Draw(sb, $"Gender: {(_selectedGender == 0 ? "Male" : "Female")}",
                new Vector2(40, 140), Color.White);
            font.Draw(sb, $"Skin {Cur(Skins, _skinIndex)}   Face {Cur(GenderFaces(), _faceIndex)}   Hair {Cur(GenderHairs(), _hairIndex)} (color {Cur(HairColors, _hairColorIndex)})",
                new Vector2(40, h - 160), Color.LightGray);
            font.Draw(sb, $"Coat {Cur(GenderCoats(), _coatIndex)}   Pants {Cur(GenderPants(), _pantsIndex)}   Shoes {Cur(Shoes, _shoesIndex)}   Weapon {Cur(Weapons, _weaponIndex)}",
                new Vector2(40, h - 140), Color.LightGray);
            font.Draw(sb, "Type to enter name. Tab toggles gender. Arrows + 1..7 cycle look.",
                new Vector2(40, h - 110), Color.LightGray);
        }
    }

    private void DrawConfirm(SpriteBatch sb, int w, int h)
    {
        if (Game.Font is not null)
        {
            Game.Font.Draw(sb, $"Create {_name}? [Y / N]", new Vector2(w / 2f - 80, h / 2f - 40), Color.White);
        }
        foreach (var b in _confirmButtons)
        {
            b.Draw(sb);
        }
    }

    public override void OnTextInput(char ch)
    {
        if (_phase != Phase.Customize)
        {
            return;
        }
        // Filter to ASCII letters + digits, no spaces. Kinoko's regex matches [A-Za-z0-9]{4,12}.
        if (ch == '\b')
        {
            if (_name.Length > 0)
            {
                _name = _name[..^1];
            }
        }
        else if (_name.Length < 12 && (char.IsLetterOrDigit(ch)))
        {
            _name += ch;
        }
    }

    public override void OnKeyPress(Keys key)
    {
        if (_phase == Phase.Customize)
        {
            switch (key)
            {
                case Keys.Tab:
                    _selectedGender = (byte)(1 - _selectedGender);
                    _faceIndex = _hairIndex = _coatIndex = _pantsIndex = 0;
                    break;
                case Keys.Left:
                    _hairIndex = (_hairIndex + GenderHairs().Length - 1) % GenderHairs().Length;
                    break;
                case Keys.Right:
                    _hairIndex = (_hairIndex + 1) % GenderHairs().Length;
                    break;
                case Keys.Up:
                    _faceIndex = (_faceIndex + 1) % GenderFaces().Length;
                    break;
                case Keys.Down:
                    _faceIndex = (_faceIndex + GenderFaces().Length - 1) % GenderFaces().Length;
                    break;
                case Keys.D1:
                    _skinIndex = (_skinIndex + 1) % Skins.Length;
                    break;
                case Keys.D2:
                    _hairColorIndex = (_hairColorIndex + 1) % HairColors.Length;
                    break;
                case Keys.D3:
                    _coatIndex = (_coatIndex + 1) % GenderCoats().Length;
                    break;
                case Keys.D4:
                    _pantsIndex = (_pantsIndex + 1) % GenderPants().Length;
                    break;
                case Keys.D5:
                    _shoesIndex = (_shoesIndex + 1) % Shoes.Length;
                    break;
                case Keys.D6:
                    _weaponIndex = (_weaponIndex + 1) % Weapons.Length;
                    break;
                case Keys.Enter:
                    SubmitCheckDuplicatedId();
                    break;
                case Keys.Back when _name.Length > 0:
                    _name = _name[..^1];
                    break;
            }
        }
        else if (_phase == Phase.Confirm)
        {
            if (key == Keys.Y || key == Keys.Enter)
            {
                SendCreateNewCharacter();
            }
            else if (key == Keys.N || key == Keys.Escape)
            {
                _phase = Phase.Customize;
            }
        }
    }

    public override void OnMouseButton(int x, int y, bool down, MouseButton button)
    {
        if (button != MouseButton.Left)
        {
            return;
        }
        var pool = _phase switch
        {
            Phase.RaceSelect => _raceButtons,
            Phase.Customize => _customizeButtons,
            Phase.Confirm => _confirmButtons,
            _ => new List<Button>(),
        };
        foreach (var b in pool)
        {
            if (b.HandleMouseButton(x, y, down))
            {
                return;
            }
        }
    }

    private AvatarLook BuildPreviewLook()
    {
        var look = new AvatarLook
        {
            Gender = _selectedGender,
            Skin = (byte)Cur(Skins, _skinIndex),
            Face = Cur(GenderFaces(), _faceIndex),
            Hair = Cur(GenderHairs(), _hairIndex) + Cur(HairColors, _hairColorIndex),
        };
        look.HairEquip[BodyPartSlot.Clothes] = Cur(GenderCoats(), _coatIndex);
        look.HairEquip[BodyPartSlot.Pants] = Cur(GenderPants(), _pantsIndex);
        look.HairEquip[BodyPartSlot.Shoes] = Cur(Shoes, _shoesIndex);
        look.HairEquip[BodyPartSlot.Weapon] = Cur(Weapons, _weaponIndex);
        return look;
    }

    private void SubmitCheckDuplicatedId()
    {
        if (_name.Length < 4)
        {
            _statusLabel = "Name must be at least 4 characters.";
            return;
        }
        var p = OutPacket.Of(InHeader.CheckDuplicatedID);
        p.WriteString(_name);
        Game.Session.Send(p);
        _statusLabel = "Checking name availability...";
    }

    private void OnCheckDuplicatedId(CheckDuplicatedIdArgs args)
    {
        if (args.Name != _name)
        {
            return;
        }
        switch (args.ResultCode)
        {
            case 0:
                _phase = Phase.Confirm;
                _statusLabel = $"'{_name}' is available — confirm?";
                break;
            case 1:
                _statusLabel = "Name is already taken.";
                break;
            default:
                _statusLabel = "Name not allowed.";
                break;
        }
    }

    private void SendCreateNewCharacter()
    {
        var p = OutPacket.Of(InHeader.CreateNewCharacter);
        p.WriteString(_name);
        p.WriteInt(NormalisedRace());
        p.WriteShort(_selectedRace == 5 ? (short)1 : (short)0);   // subJob: 1 only for Dual Blade
        p.WriteInt(Cur(GenderFaces(), _faceIndex));
        p.WriteInt(Cur(GenderHairs(), _hairIndex));
        p.WriteInt(Cur(HairColors, _hairColorIndex));
        p.WriteInt(Cur(Skins, _skinIndex));
        p.WriteInt(Cur(GenderCoats(), _coatIndex));
        p.WriteInt(Cur(GenderPants(), _pantsIndex));
        p.WriteInt(Cur(Shoes, _shoesIndex));
        p.WriteInt(Cur(Weapons, _weaponIndex));
        p.WriteByte(_selectedGender);
        Game.Session.Send(p);
        _statusLabel = "Creating character...";
    }

    private int NormalisedRace() => _selectedRace switch
    {
        5 => 1,                  // Dual Blade → race=NORMAL with subJob=1 below
        _ => _selectedRace,
    };

    private void OnCreateCharacterResult(CreateNewCharacterResultArgs args)
    {
        if (args.Success && args.Entry != null)
        {
            _statusLabel = $"Created {args.Entry.Stat.Name}!";
            Game.StageDirector.Replace(new CharSelectStage(
                _loggerFactory.CreateLogger<CharSelectStage>(),
                _loggerFactory, _ui, _map, _sound, _existingCharacters));
        }
        else
        {
            _statusLabel = $"Create failed (code {args.ResultCode}).";
            _phase = Phase.Customize;
        }
    }

    // ---- Layout helpers ----
    private int[] GenderFaces() => _selectedGender == 0 ? MaleFaces : FemaleFaces;
    private int[] GenderHairs() => _selectedGender == 0 ? MaleHairs : FemaleHairs;
    private int[] GenderCoats() => _selectedGender == 0 ? MaleCoats : FemaleCoats;
    private int[] GenderPants() => _selectedGender == 0 ? MalePants : FemalePants;
    private static int Cur(int[] arr, int idx) => arr[((idx % arr.Length) + arr.Length) % arr.Length];

    private void BuildRaceButtons()
    {
        // Six race buttons stacked vertically on the right side; clicking sets _selectedRace and advances to Customize.
        var labels = new (string Name, int Race, string Path)[]
        {
            ("Normal", 1, "Login.img/RaceSelect/BtNormal"),
            ("Cygnus", 2, "Login.img/RaceSelect/BtKnight"),
            ("Aran", 3, "Login.img/RaceSelect/BtAran"),
            ("Evan", 4, "Login.img/RaceSelect/BtEvan"),
            ("Resistance", 0, "Login.img/RaceSelect/BtResistance"),
            ("Dual", 5, "Login.img/RaceSelect/BtDual"),
        };
        for (var i = 0; i < labels.Length; i++)
        {
            var lbl = labels[i];
            var pos = new Vector2(560, 140 + i * 60);
            var btn = MakeButton(lbl.Path, pos, () =>
            {
                _selectedRace = lbl.Race;
                _statusLabel = $"Selected: {lbl.Name}";
                _phase = Phase.Customize;
                _nameField = new TextField
                {
                    Position = new Vector2(40, 110),
                    Width = 240,
                    Height = 24,
                    Font = Game.Font,
                    MaxLength = 12,
                    IsFocused = true,
                };
            }) ?? MakeFallbackButton(lbl.Name, pos, () =>
            {
                _selectedRace = lbl.Race;
                _phase = Phase.Customize;
                _statusLabel = $"Selected: {lbl.Name}";
            });
            _raceButtons.Add(btn);
        }
    }

    private void BuildCustomizeButtons()
    {
        _customizeButtons.Add(MakeFallbackButton("OK", new Vector2(680, 530),
            SubmitCheckDuplicatedId));
        _customizeButtons.Add(MakeFallbackButton("Cancel", new Vector2(580, 530),
            () => _phase = Phase.RaceSelect));
        _confirmButtons.Add(MakeFallbackButton("Yes", new Vector2(420, 380), SendCreateNewCharacter));
        _confirmButtons.Add(MakeFallbackButton("No", new Vector2(520, 380),
            () => _phase = Phase.Customize));
    }

    private Button MakeFallbackButton(string label, Vector2 pos, Action onClick)
    {
        var b = new Button(_loader!, buttonRoot: null)
        {
            Position = pos,
            OnClick = onClick,
            FallbackLabel = label,
            Font = Game.Font,
            WhitePixel = Game.WhitePixel,
        };
        return b;
    }

    private WzSprite? LoadCanvas(string path)
    {
        try
        {
            return _ui?.GetItem(path) is WzCanvas c ? _loader!.Load(c) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CharCreate: missing canvas {Path}", path);
            return null;
        }
    }

    private Button? MakeButton(string path, Vector2 pos, Action onClick)
    {
        var root = _ui?.GetItem(path) as WzProperty;
        if (root is null)
        {
            return null;
        }
        return new Button(_loader!, root) { Position = pos, OnClick = onClick };
    }
}
