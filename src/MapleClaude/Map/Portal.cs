using Microsoft.Xna.Framework;

namespace MapleClaude.Map;

/// <summary>One portal entry parsed from <c>...img/portal/&lt;n&gt;</c>.</summary>
public sealed class Portal
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int TargetMap { get; init; }
    public string TargetPortal { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public int Delay { get; init; }
    public bool OnlyOnce { get; init; }

    public Vector2 Position => new(X, Y);
}
