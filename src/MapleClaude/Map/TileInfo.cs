using MapleClaude.Render;

namespace MapleClaude.Map;

/// <summary>One entry from <c>...img/&lt;layer&gt;/tile/&lt;n&gt;</c>.</summary>
public sealed class TileInfo
{
    public int X { get; init; }
    public int Y { get; init; }
    public string TilesetKey { get; init; } = string.Empty; // u (e.g. "wood")
    public int No { get; init; }
    public int ZM { get; init; }

    public WzSprite? Sprite { get; set; }
}
