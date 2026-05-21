using MapleClaude.Map;
using MapleClaude.Net.Packet;
using Microsoft.Xna.Framework;

namespace MapleClaude.Character;

/// <summary>
/// Simple platformer physics + foothold ground-snap. Owns the player's
/// world position, velocity, stance, and animation frame, and emits
/// move-path entries that GameStage packages into outgoing
/// <c>UserMove(44)</c> packets.
/// </summary>
public sealed class PlayerController
{
    private const float WalkSpeed = 140f;
    private const float JumpSpeed = 555f;
    private const float Gravity = 2000f;
    private const float MaxFallSpeed = 670f;
    private const float MovePathFlushSeconds = 0.10f;
    private const int MaxMovePathElements = 12;

    private readonly FieldScene _field;
    private Vector2 _velocity;
    private bool _grounded;
    private int _currentFoothold;
    private float _animTimer;
    private float _movePathTimer;
    private readonly List<MoveElement> _pendingElements = new();
    private Vector2 _lastSyncedPos;

    public Vector2 Position { get; set; }
    public Stance Stance { get; private set; } = Stance.Stand1;
    public int Frame { get; private set; }
    public bool FacingLeft { get; private set; }

    public PlayerController(FieldScene field)
    {
        _field = field;
    }

    public void Update(PlayerInput input, float dt)
    {
        // Horizontal motion.
        var moveDir = (input.Left ? -1 : 0) + (input.Right ? 1 : 0);
        if (moveDir != 0)
        {
            _velocity = new Vector2(WalkSpeed * moveDir, _velocity.Y);
            FacingLeft = moveDir < 0;
        }
        else
        {
            _velocity = new Vector2(_velocity.X * 0.6f, _velocity.Y);
            if (Math.Abs(_velocity.X) < 1f)
            {
                _velocity = new Vector2(0f, _velocity.Y);
            }
        }

        // Jump on alt-edge while grounded.
        if (input.JumpPressed && _grounded)
        {
            _velocity = new Vector2(_velocity.X, -JumpSpeed);
            _grounded = false;
        }

        // Gravity.
        if (!_grounded)
        {
            _velocity = new Vector2(_velocity.X, Math.Min(_velocity.Y + Gravity * dt, MaxFallSpeed));
        }

        Position += _velocity * dt;
        SnapToFoothold();

        if (!_grounded)
        {
            Stance = Stance.Jump;
        }
        else if (Math.Abs(_velocity.X) > 1f)
        {
            Stance = Stance.Walk1;
        }
        else
        {
            Stance = Stance.Stand1;
        }

        _animTimer += dt;
        if (_animTimer >= 0.18f)
        {
            _animTimer -= 0.18f;
            Frame = (Frame + 1) % 4;
        }

        // Accumulate a NORMAL move-path entry roughly every flush window.
        _movePathTimer += dt;
        if (_movePathTimer >= MovePathFlushSeconds || _pendingElements.Count >= MaxMovePathElements)
        {
            AppendCurrentSample();
        }
    }

    private void SnapToFoothold()
    {
        if (_field.Footholds.Count == 0)
        {
            // No footholds loaded yet — pretend we're always grounded so we don't fall through space.
            _grounded = true;
            return;
        }
        Foothold? best = null;
        var bestDistance = float.PositiveInfinity;
        foreach (var (_, fh) in _field.Footholds)
        {
            var y = fh.YAt(Position.X);
            if (y is null)
            {
                continue;
            }
            var dy = y.Value - Position.Y;
            if (dy >= -2 && dy < bestDistance)
            {
                bestDistance = dy;
                best = fh;
            }
        }
        if (best is null)
        {
            _grounded = false;
            return;
        }
        var fhY = best.YAt(Position.X) ?? Position.Y;
        if (Position.Y >= fhY - 2)
        {
            Position = new Vector2(Position.X, fhY);
            _velocity = new Vector2(_velocity.X, 0f);
            _grounded = true;
            _currentFoothold = best.Id;
        }
        else
        {
            _grounded = false;
        }
    }

    private void AppendCurrentSample()
    {
        var elapsedMs = (short)Math.Clamp(_movePathTimer * 1000f, 1, 1000);
        var elem = new MoveElement
        {
            Attr = 0,                  // NORMAL
            X = (short)Position.X,
            Y = (short)Position.Y,
            Vx = (short)_velocity.X,
            Vy = (short)_velocity.Y,
            Fh = (short)_currentFoothold,
            MoveAction = 0,
            Elapse = elapsedMs,
        };
        _pendingElements.Add(elem);
        _movePathTimer = 0f;
        _lastSyncedPos = Position;
    }

    /// <summary>Try to flush the pending move-path. Returns true + blob if there were elements to send.</summary>
    public bool TryFlushMovePath(out byte[] blob)
    {
        if (_pendingElements.Count == 0)
        {
            blob = Array.Empty<byte>();
            return false;
        }
        blob = MovePathEncoder.Encode(
            (short)_lastSyncedPos.X, (short)_lastSyncedPos.Y,
            (short)_velocity.X, (short)_velocity.Y,
            _pendingElements);
        _pendingElements.Clear();
        return true;
    }
}
