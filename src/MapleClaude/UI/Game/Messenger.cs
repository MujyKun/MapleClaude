using MapleClaude.Render;
using MapleClaude.UI;
using MapleClaude.Wz;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

/// <summary>
/// Maple Messenger window — a 3-person chat room. This panel shows the three
/// participant slots; chat is sent and shown through the main <c>ChatBar</c>
/// (via the <c>/m</c> command), mirroring how party/buddy/guild chat works.
/// WZ: <c>UIWindow.img/MemoChat</c>/messenger assets vary by version, so this
/// draws a simple framed window with a fallback. Opened with <c>/messenger</c>.
/// </summary>
public sealed class Messenger : GamePanel
{
    private readonly BuiltInFont? _font;
    private readonly Button? _btClose;
    private readonly List<Button> _allButtons = new();

    // Up to 3 participants, keyed by the server's user index (0..2).
    private readonly string?[] _slots = new string?[3];
    private int _selfIndex = -1;

    /// <summary>Fired when the window is closed (send Leave).</summary>
    public Action? OnClosed { get; set; }

    private const int PanelW = 180;
    private const int PanelH = 150;

    public Messenger(WzTextureLoader loader, WzPackage? ui, BuiltInFont? font)
    {
        _font = font;
        IsVisible = false;
        Position = new Vector2(300, 120);

        var win = ui?.GetItem("UIWindow.img/MemoChat") as WzProperty;
        _btClose = MakeButton(loader, win, "BtClose", Close);

        ApplyLayout();
    }

    public void Open()
    {
        IsVisible = true;
    }

    public void SetSelf(int index)
    {
        _selfIndex = index;
        if (index is >= 0 and < 3) _slots[index] = "(you)";
        IsVisible = true;
    }

    public void SetParticipant(int index, string name)
    {
        if (index is >= 0 and < 3) _slots[index] = name;
    }

    public void RemoveParticipant(int index)
    {
        if (index is >= 0 and < 3) _slots[index] = null;
    }

    public void Reset()
    {
        for (var i = 0; i < _slots.Length; i++) _slots[i] = null;
        _selfIndex = -1;
    }

    private void Close()
    {
        IsVisible = false;
        Reset();
        OnClosed?.Invoke();
    }

    private void ApplyLayout()
    {
        if (_btClose != null) _btClose.Position = Position + new Vector2(PanelW - 18, 4);
    }

    public override void Update(GameTime gameTime) => ApplyLayout();

    public override void Draw(SpriteBatch sb, Texture2D white)
    {
        if (!IsVisible) return;
        ApplyLayout();

        var px = (int)Position.X;
        var py = (int)Position.Y;

        sb.Draw(white, new Rectangle(px, py, PanelW, PanelH), new Color(15, 15, 25, 235));
        DrawBorder(sb, white, new Rectangle(px, py, PanelW, PanelH));
        _font?.Draw(sb, "Maple Messenger", new Vector2(px + 8, py + 5), new Color(220, 200, 150));
        sb.Draw(white, new Rectangle(px, py + 22, PanelW, 1), new Color(80, 70, 50));

        for (var i = 0; i < _slots.Length; i++)
        {
            var sy = py + 30 + i * 34;
            var occupied = !string.IsNullOrEmpty(_slots[i]);
            sb.Draw(white, new Rectangle(px + 8, sy, PanelW - 16, 30),
                occupied ? new Color(35, 35, 60, 220) : new Color(22, 22, 34, 180));
            DrawBorder(sb, white, new Rectangle(px + 8, sy, PanelW - 16, 30));
            var label = occupied ? _slots[i]! : "(empty)";
            var color = i == _selfIndex ? new Color(255, 220, 120)
                      : occupied ? Color.White : new Color(110, 110, 130);
            _font?.Draw(sb, label, new Vector2(px + 16, sy + 9), color);
        }

        _font?.Draw(sb, "Chat with /m <text>", new Vector2(px + 8, py + PanelH - 16), new Color(130, 130, 150));

        foreach (var b in _allButtons) b.Draw(sb);
    }

    public override bool HandleMouseButton(int x, int y, bool down)
    {
        if (!IsVisible) return false;
        foreach (var b in _allButtons)
            if (b.HandleMouseButton(x, y, down)) return true;
        return new Rectangle((int)Position.X, (int)Position.Y, PanelW, PanelH).Contains(x, y);
    }

    public override bool OnKeyPress(Keys key)
    {
        if (!IsVisible) return false;
        if (key == Keys.Escape) { Close(); return true; }
        return false;
    }

    private Button? MakeButton(WzTextureLoader loader, WzProperty? root, string name, Action onClick)
    {
        var pr = root?.Get(name) as WzProperty;
        if (pr is null) return null;
        var b = new Button(loader, pr) { OnClick = onClick };
        _allButtons.Add(b);
        return b;
    }

    private static void DrawBorder(SpriteBatch sb, Texture2D white, Rectangle r)
    {
        var c = new Color(80, 70, 50);
        sb.Draw(white, new Rectangle(r.X, r.Y, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
        sb.Draw(white, new Rectangle(r.X, r.Y, 1, r.Height), c);
        sb.Draw(white, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
    }
}
