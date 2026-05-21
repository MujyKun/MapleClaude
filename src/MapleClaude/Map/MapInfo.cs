namespace MapleClaude.Map;

/// <summary>Decoded subset of <c>...img/info</c>. Phase-2 needs bgm + VR bounds (camera clamp).</summary>
public sealed class MapInfo
{
    public string Bgm { get; init; } = string.Empty;
    public int ReturnMap { get; init; }
    public int ForcedReturn { get; init; }
    public int FieldLimit { get; init; }
    public string MapDesc { get; init; } = string.Empty;
    public int Town { get; init; }
    public int VRLeft { get; init; }
    public int VRTop { get; init; }
    public int VRRight { get; init; }
    public int VRBottom { get; init; }
    public bool HasVR => VRRight > VRLeft && VRBottom > VRTop;
}
