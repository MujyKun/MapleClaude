using MapleClaude.Domain;
using Microsoft.Xna.Framework;

namespace MapleClaude.Map;

/// <summary>One portal entry parsed from <c>...img/portal/&lt;n&gt;</c>.</summary>
public sealed class Portal
{
    private const int NoTargetMap = 999_999_999;

    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int TargetMap { get; init; }
    public string TargetPortal { get; init; } = string.Empty;
    public int Delay { get; init; }
    public bool OnlyOnce { get; init; }

    public Vector2 Position => new(X, Y);

    /// <summary>True when this portal can send the player to another field.</summary>
    public bool HasFieldTarget => TargetMap > 0 && TargetMap != NoTargetMap;

    /// <summary>
    /// v95 visible in-field portals use portal type 2 and the
    /// <c>MapHelper.img/portal/game/pv</c> animation. Spawn/invisible portals do
    /// not draw; hidden/script portal reveals are a later state-aware pass.
    /// </summary>
    public bool DrawsVisibleGamePortal => PortalVisual.ForType(Type) is not null;
}
