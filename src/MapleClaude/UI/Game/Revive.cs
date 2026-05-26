using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Town-revive confirmation dialog — the modal that asks "Revive in town?" after
/// the player dies and the tombstone lands. Port of the v95 client's
/// <c>CUIRevive</c> (the default branch that uses <c>aUi_351</c> as background;
/// premium variants — SoulStone <c>aUi_519</c> / Wheel of Destiny <c>aUi_274</c>
/// / quest <c>aUi_306</c> — are out of scope for v1).
///
/// <para>Authentic flow (CUIRevive::OnButtonClicked): button id 6 (Yes) → calls
/// <c>Revive(this, 1)</c>; button id 7 (No) → calls <c>Revive(this, 0)</c>. Both
/// branches send the same <c>UserTransferFieldRequest(41)</c> with empty portal
/// name; the <c>bPremium</c> byte differs. For v1 we send <c>premium=false</c>
/// (the server picks the field's <c>returnMap</c>, sets HP to 50).</para>
///
/// <para>WZ probe order (best to fallback):
/// <list type="number">
///   <item><c>UI.wz/UIWindow.img/Revive/0</c> (default v95 layout)</item>
///   <item><c>UI.wz/UIWindow.img/Revive</c> (some clients place backgrnd at the root)</item>
///   <item><c>UI.wz/UIWindow.img/FadeYesNo</c> (generic Yes/Cancel dialog used by other modals)</item>
///   <item>Plain panel fallback (drawn rectangle + built-in font) when no asset is found</item>
/// </list>
/// </para>
/// </summary>
public sealed class Revive : GamePanel
{
    private const int FallbackPanelW = 320;
    private const int FallbackPanelH = 140;
    private const float FadeSeconds = 0.20f;

    private readonly WzSprite? _backgrnd;
    private readonly Button _btOk;
    private readonly BuiltInFont? _font;

    private int _viewW = 1024, _viewH = 768;
    private float _alpha;
    // Anti-spam: when first opened we ignore clicks for a few frames so a held
    // mouse button from the moment of death doesn't auto-click the OK button.
    private float _ignoreInputMs;

    /// <summary>Fires when the user accepts revival. <c>premium=false</c> for the
    /// default town revive (server picks returnMap, HP=50); <c>true</c> for the
    /// SoulStone / Wheel of Destiny path (unused in v1 but threaded through).</summary>
    public Action<bool>? OnRevive { get; set; }

    public Revive(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font, Texture2D? whitePixel = null)
    {
        _font = font;
        IsVisible = false;

        // Probe — pick the first layout that exists.
        var (root, backgrnd, btYes) = ProbeAssets(ui);
        _backgrnd = backgrnd is not null ? loader.Load(backgrnd) : null;
        _btOk = new Button(loader, btYes)
        {
            FallbackLabel = "OK",
            FallbackSize  = new Point(72, 24),
            Font          = font,
            WhitePixel    = whitePixel,
            OnClick       = AcceptTownRevive,
        };
        _ = root;   // currently unused — kept for future button-position introspection
    }

    /// <summary>Show the dialog and reset the fade-in / click-debounce timers.</summary>
    public void Open()
    {
        IsVisible = true;
        _alpha = 0f;
        _ignoreInputMs = 250f;   // ¼ s grace so a held click at death doesn't auto-confirm
    }

    /// <summary>Hide and reset (called after a successful revive or on field swap).</summary>
    public void Close()
    {
        IsVisible = false;
        _alpha = 0f;
        _ignoreInputMs = 0f;
    }

    private void AcceptTownRevive()
    {
        if (_ignoreInputMs > 0f) return;
        Close();
        OnRevive?.Invoke(false);   // premium=false → town revive with HP=50
    }

    public override void Relayout(int viewWidth, int viewHeight)
    {
        _viewW = viewWidth;
        _viewH = viewHeight;
    }

    public override void Update(GameTime gameTime)
    {
        if (!IsVisible) { _alpha = 0f; return; }
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var ms = dt * 1000f;
        _alpha = Math.Min(1f, _alpha + dt / FadeSeconds);
        if (_ignoreInputMs > 0f) _ignoreInputMs -= ms;

        // Place the OK button each frame (the panel may move on resize).
        _btOk.Position = ButtonAnchor();
        var mouse = Mouse.GetState();
        _btOk.Update(mouse.X, mouse.Y, mouse.LeftButton == ButtonState.Pressed);
    }

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;
        var top  = TopLeft();
        var tint = Color.White * _alpha;
        var bounds = new Rectangle((int)top.X, (int)top.Y, PanelWidth, PanelHeight);

        if (_backgrnd is not null)
        {
            _backgrnd.Draw(sb, top, tint);
        }
        else
        {
            // No WZ asset: draw a centered dark panel with a coloured border.
            sb.Draw(white, bounds, new Color(20, 24, 32) * _alpha);
            DrawBorder(sb, white, bounds, new Color(160, 60, 60) * _alpha);
        }

        // Always overlay the question text — even with a WZ background, the body
        // copy is rendered by client code (the v95 CUIRevive draws localised text
        // on top of its background canvas).
        if (_font is not null)
        {
            const string title = "You died.";
            const string body  = "Revive in town?";
            var ts = _font.Measure(title);
            var bs = _font.Measure(body);
            _font.Draw(sb, title, new Vector2(bounds.X + (bounds.Width - ts.X) / 2f, bounds.Y + 24), Color.White * _alpha);
            _font.Draw(sb, body,  new Vector2(bounds.X + (bounds.Width - bs.X) / 2f, bounds.Y + 50), Color.White * _alpha);
        }
        _btOk.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        // Always swallow input while the dialog is up (modal).
        _btOk.HandleMouseButton(x, y, down);
        return true;
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;
        // Enter / Space / Y confirm; Escape is a no-op so a panicked ESC doesn't
        // accidentally close the dialog (it can only be dismissed by accepting).
        if (key is Keys.Enter or Keys.Space or Keys.Y)
        {
            AcceptTownRevive();
        }
        return true;   // swallow every key while open
    }

    // ── Internal helpers ────────────────────────────────────────────────────────

    private int PanelWidth  => _backgrnd?.Width  ?? FallbackPanelW;
    private int PanelHeight => _backgrnd?.Height ?? FallbackPanelH;

    private Vector2 TopLeft()
    {
        // Centered horizontally; slightly above vertical center (matches v95
        // CUIRevive placement, which sits above the screen midline).
        var x = (_viewW - PanelWidth)  / 2f;
        var y = (_viewH - PanelHeight) / 2f - 40;
        return new Vector2(x, y);
    }

    private Vector2 ButtonAnchor()
    {
        // Center the OK button along the panel's lower edge.
        var top = TopLeft();
        return new Vector2(top.X + PanelWidth / 2f - 36, top.Y + PanelHeight - 32);
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r, Color c)
    {
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }

    // Probe UI.wz for the dialog assets, returning the most authentic match.
    // The v95 client's CUIRevive uses StringPool-resolved paths (aUi_274, aUi_351,
    // aUi_519, aUi_306) — these don't appear in our localization CSV, so probe by
    // path; degrade through FadeYesNo to no-asset fallback.
    private static (WzProperty? root, WzCanvas? backgrnd, WzProperty? btYes) ProbeAssets(WzPackage? ui)
    {
        if (ui is null) return (null, null, null);

        // v95 has no dedicated Revive node in UIWindow.img — the CUIRevive
        // backgrounds (aUi_274/aUi_351/aUi_519/aUi_306) point to inline-string
        // paths we haven't resolved yet. Probe the obvious candidates; on a miss
        // the panel falls back to a clean dark rectangle with body text. We
        // deliberately skip FadeYesNo because its 202×56 single-line frame is
        // too small for the multi-line revive message.
        var candidates = new[]
        {
            "UIWindow.img/Revive/0",
            "UIWindow.img/Revive",
        };
        foreach (var path in candidates)
        {
            if (ui.GetItem(path) is not WzProperty root) continue;
            var bg = root.Get("backgrnd") as WzCanvas;
            var bt = (root.Get("BtYes") as WzProperty)
                  ?? (root.Get("BtOK") as WzProperty)
                  ?? (root.Get("BtRevive") as WzProperty);
            if (bg is not null || bt is not null)
            {
                return (root, bg, bt);
            }
        }
        return (null, null, null);
    }
}
