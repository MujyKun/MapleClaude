namespace MapleClaude.Character;

/// <summary>Held / edge state for one Update tick. Set by FieldStage from MonoGame keyboard input.</summary>
public readonly record struct PlayerInput
{
    public bool Left { get; init; }
    public bool Right { get; init; }
    public bool Up { get; init; }
    public bool Down { get; init; }
    public bool JumpPressed { get; init; }
}
