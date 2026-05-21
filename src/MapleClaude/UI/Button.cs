using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.UI;

/// <summary>
/// 4-state interactive button backed by a WZ <c>Bt*</c> subtree
/// (<c>normal/0</c>, <c>mouseOver/0</c>, <c>pressed/0</c>, <c>disabled/0</c>).
/// Tracks pointer hover / down state. Fires <see cref="OnClick"/> when the
/// user releases the mouse over the button after pressing it.
/// </summary>
public sealed class Button
{
    private readonly WzSprite? _normal;
    private readonly WzSprite? _hover;
    private readonly WzSprite? _pressed;
    private readonly WzSprite? _disabled;

    private bool _isHover;
    private bool _isPressed;

    public Vector2 Position { get; set; }
    public bool Enabled { get; set; } = true;
    public Action? OnClick { get; set; }

    /// <summary>If set, the button renders a label-on-rectangle fallback when the WZ sprites are unavailable.</summary>
    public string? FallbackLabel { get; set; }

    /// <summary>Fallback hit-area size when FallbackLabel is set.</summary>
    public Microsoft.Xna.Framework.Point FallbackSize { get; set; } = new(72, 24);

    /// <summary>Optional font used to render <see cref="FallbackLabel"/>.</summary>
    public BuiltInFont? Font { get; set; }

    /// <summary>Optional 1x1 texture used to render the fallback rectangle.</summary>
    public Texture2D? WhitePixel { get; set; }

    public Button(WzTextureLoader loader, WzProperty? buttonRoot)
    {
        _normal = LoadFirst(loader, buttonRoot, "normal");
        _hover = LoadFirst(loader, buttonRoot, "mouseOver");
        _pressed = LoadFirst(loader, buttonRoot, "pressed");
        _disabled = LoadFirst(loader, buttonRoot, "disabled");
    }

    public Rectangle Bounds
    {
        get
        {
            if (_normal != null)
            {
                return new Rectangle(
                    (int)(Position.X - _normal.Origin.X),
                    (int)(Position.Y - _normal.Origin.Y),
                    _normal.Width,
                    _normal.Height);
            }
            if (FallbackLabel is not null)
            {
                return new Rectangle((int)Position.X, (int)Position.Y, FallbackSize.X, FallbackSize.Y);
            }
            return new Rectangle((int)Position.X, (int)Position.Y, 0, 0);
        }
    }

    public void Update(int mouseX, int mouseY, bool mouseDown)
    {
        _isHover = Bounds.Contains(mouseX, mouseY);
        if (!_isHover)
        {
            _isPressed = false;
        }
        else if (mouseDown && !_isPressed)
        {
            // (transition handled in OnMouseButton)
        }
    }

    /// <summary>Returns true if the click was inside this button (and triggers <see cref="OnClick"/>).</summary>
    public bool HandleMouseButton(int x, int y, bool down)
    {
        var inside = Bounds.Contains(x, y);
        if (down)
        {
            if (inside && Enabled)
            {
                _isPressed = true;
                return true;
            }
        }
        else
        {
            // Up: if we were pressed and we're still inside, fire.
            var wasPressed = _isPressed;
            _isPressed = false;
            if (wasPressed && inside && Enabled)
            {
                OnClick?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void Draw(SpriteBatch sb)
    {
        var sprite = !Enabled ? (_disabled ?? _normal)
                     : _isPressed ? (_pressed ?? _normal)
                     : _isHover ? (_hover ?? _normal)
                     : _normal;
        if (sprite is not null)
        {
            sprite.Draw(sb, Position);
            return;
        }
        if (FallbackLabel is not null && WhitePixel is not null)
        {
            var fillColor = !Enabled ? new Color(40, 40, 40)
                : _isPressed ? new Color(80, 80, 120)
                : _isHover ? new Color(120, 140, 200)
                : new Color(70, 90, 140);
            sb.Draw(WhitePixel, Bounds, fillColor);
            if (Font is not null)
            {
                var size = Font.Measure(FallbackLabel);
                var labelPos = new Vector2(
                    Bounds.X + (Bounds.Width - size.X) / 2f,
                    Bounds.Y + (Bounds.Height - Font.LineHeight) / 2f);
                Font.Draw(sb, FallbackLabel, labelPos, Color.White);
            }
        }
    }

    private static WzSprite? LoadFirst(WzTextureLoader loader, WzProperty? root, string state)
    {
        if (root?.Get(state) is not WzProperty statePr)
        {
            return null;
        }
        if (statePr.Get("0") is WzCanvas canvas)
        {
            return loader.Load(canvas);
        }
        return null;
    }
}
