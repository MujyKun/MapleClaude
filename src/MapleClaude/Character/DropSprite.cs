using MapleClaude.Render;
using MapleClaude.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Character;

/// <summary>
/// A dropped item or meso bag on the ground.
/// Bounces in with a small arc animation on spawn, then sits still.
/// Picked up by <c>DropLeaveField</c> which removes it from the world list.
/// </summary>
public sealed class DropSprite
{
    public int     DropId          { get; }
    public bool    IsMoney         { get; }
    public int     ItemIdOrAmount  { get; }
    public Vector2 Position        { get; set; }

    // Drop-in arc (visual only — 0.4s)
    private float  _age;
    private const float ArcDuration = 0.4f;
    private const float ArcHeight   = 30f;
    private readonly Vector2 _groundPos;

    private readonly BuiltInFont? _font;

    // Meso amounts colour-coded
    private static readonly (int min, Color color)[] MesoColors =
    [
        (1,       new Color(220, 200, 100)),   // bronze coin
        (1000,    new Color(200, 200, 200)),   // silver coin
        (10000,   new Color(255, 215, 0)),     // gold coin
        (100000,  new Color(255, 100, 100)),   // red coin
    ];

    public DropSprite(int dropId, bool isMoney, int itemIdOrAmount, Vector2 position, BuiltInFont? font)
    {
        DropId         = dropId;
        IsMoney        = isMoney;
        ItemIdOrAmount = itemIdOrAmount;
        _groundPos     = position;
        Position       = position - new Vector2(0, ArcHeight);  // start above ground
        _font          = font;
    }

    public void Update(float dt)
    {
        if (_age < ArcDuration)
        {
            _age = Math.Min(_age + dt, ArcDuration);
            var t      = _age / ArcDuration;
            var arcY   = ArcHeight * (1 - 4 * (t - 0.5f) * (t - 0.5f)); // parabola peak at t=0.5
            Position   = _groundPos - new Vector2(0, arcY);
        }
        else
        {
            Position = _groundPos;
        }
    }

    public void Draw(SpriteBatch sb, Texture2D white, Vector2 screenPos)
    {
        const int IconW = 20;
        const int IconH = 20;
        var ix = (int)(screenPos.X - IconW / 2f);
        var iy = (int)(screenPos.Y - IconH);

        if (IsMoney)
        {
            // Coin — colour based on amount
            var coinColor = MesoColors[0].color;
            foreach (var (min, col) in MesoColors)
                if (ItemIdOrAmount >= min) coinColor = col;
            sb.Draw(white, new Rectangle(ix, iy, IconW, IconH), coinColor);
            _font?.Draw(sb, "₩", new Vector2(ix + 4, iy + 3), new Color(0, 0, 0, 200));
        }
        else
        {
            // Item icon placeholder — tinted by item type (equip=blue, use=green, etc.)
            var invType = ItemIdOrAmount / 1_000_000;
            var itemColor = invType switch
            {
                1 => new Color(80,  120, 200, 220),  // equip
                2 => new Color(80,  180, 80,  220),  // use
                3 => new Color(160, 120, 60,  220),  // setup
                4 => new Color(150, 150, 150, 220),  // etc
                5 => new Color(200, 80,  200, 220),  // cash
                _ => new Color(140, 140, 140, 220),
            };
            sb.Draw(white, new Rectangle(ix, iy, IconW, IconH), itemColor);
            // First two digits of item ID as icon text
            _font?.Draw(sb, (ItemIdOrAmount % 100).ToString("D2"), new Vector2(ix + 2, iy + 3),
                Color.White);
        }
    }
}
