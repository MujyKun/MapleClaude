using Microsoft.Xna.Framework;

namespace MapleClaude.Map;

/// <summary>
/// One line-segment foothold parsed from <c>Map.wz/Map/Map&lt;p&gt;/&lt;id&gt;.img/foothold/&lt;layer&gt;/&lt;group&gt;/&lt;id&gt;</c>.
/// Footholds are the polyline ground geometry used for collision +
/// ground-snap during movement.
/// </summary>
public sealed class Foothold
{
    public int Id { get; init; }
    public int Layer { get; init; }
    public int Group { get; init; }
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; }
    public int Y2 { get; init; }
    public int Prev { get; init; }
    public int Next { get; init; }
    public bool CantThrough { get; init; }
    public bool ForbidFallDown { get; init; }
    public int Force { get; init; }

    /// <summary>Slope this foothold makes (used by physics). Returns 0 for horizontal.</summary>
    public float Slope => X1 == X2 ? 0f : (Y2 - Y1) / (float)(X2 - X1);

    /// <summary>Returns the foothold Y at world X (linear interpolation). null if X is outside [X1,X2].</summary>
    public float? YAt(float x)
    {
        var lo = Math.Min(X1, X2);
        var hi = Math.Max(X1, X2);
        if (x < lo || x > hi)
        {
            return null;
        }
        if (X1 == X2)
        {
            return Math.Min(Y1, Y2);
        }
        var t = (x - X1) / (float)(X2 - X1);
        return Y1 + t * (Y2 - Y1);
    }

    public Vector2 StartPoint => new(X1, Y1);
    public Vector2 EndPoint => new(X2, Y2);
}
