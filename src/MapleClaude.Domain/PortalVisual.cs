namespace MapleClaude.Domain;

/// <summary>
/// Client-side render metadata for Map.wz portal types. The integer values come
/// directly from each map image's <c>portal/&lt;n&gt;/pt</c> field; only visible warp
/// portals are drawn during normal gameplay. Spawn and invisible portal nodes
/// remain logic-only so the Phase 10 transfer path is unchanged.
/// </summary>
public sealed record PortalVisual(string AnimationPath)
{
    private static readonly PortalVisual Visible = new("pv");

    /// <summary>
    /// Returns the <c>MapHelper.img/portal/game/&lt;AnimationPath&gt;</c> animation to
    /// draw for this portal type, or <c>null</c> when the type is intentionally
    /// invisible in normal gameplay.
    /// </summary>
    public static PortalVisual? ForType(int portalType) => portalType switch
    {
        2 => Visible,
        _ => null,
    };
}
