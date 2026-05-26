using MapleClaude.Render;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Character;

/// <summary>
/// The falling-tombstone effect that plays when the local player's HP hits zero —
/// a port of the v95 <c>CUser::OnSetDead</c> tombstone branch. Loads the descent
/// frame sequence from <c>Effect.wz/Tomb.img/fall</c> (StringPool id
/// <c>SP_2221_EFFECT_TOMBIMG_FALL</c>) and plays it once at the foothold-snapped
/// landing point (the per-frame artwork itself depicts the descent), then swaps
/// to <c>Effect.wz/Tomb.img/land</c> for the landed state and plays the
/// <c>Sound.wz/Game.img/Tombstone</c> burial cue. Fires <see cref="OnLanded"/>
/// after the fall — that's the cue for <c>GameStage</c> to open the Revive dialog.
///
/// <para>Pattern matches <see cref="EmotionBubble"/>: a tiny self-contained world
/// effect that <c>GameStage</c> ticks each frame and draws after the player layer.
/// Missing WZ assets degrade gracefully (no sprite, no sound, callback still fires)
/// so the revive flow always reaches the dialog.</para>
/// </summary>
public sealed class TombstoneEffect
{
    private readonly WzPackage? _effectWz;
    private readonly WzPackage? _soundWz;
    private readonly WzTextureLoader _loader;
    private readonly WzAudioPlayer? _audio;
    private readonly ILogger? _logger;

    private FrameData[] _fallFrames = Array.Empty<FrameData>();
    private FrameData[] _landFrames = Array.Empty<FrameData>();
    private WzSound? _burialSound;

    private Vector2 _world;             // landing point in world space (player's foothold)
    private int _frameIndex;
    private float _frameTimerMs;
    private bool _fellDown;
    private bool _soundPlayed;

    public bool Started { get; private set; }
    public bool Landed => _fellDown;

    /// <summary>Fires once when the fall sequence finishes (or immediately on
    /// <c>Spawn</c> if no fall art is available). The Revive dialog opens here.</summary>
    public Action? OnLanded { get; set; }

    public TombstoneEffect(WzPackage? effectWz, WzPackage? soundWz, WzTextureLoader loader,
                           WzAudioPlayer? audio, ILogger? logger = null)
    {
        _effectWz = effectWz;
        _soundWz  = soundWz;
        _loader   = loader;
        _audio    = audio;
        _logger   = logger;
    }

    /// <summary>Begin the fall at <paramref name="foothold"/> (world space — the
    /// player's foot point). Repeat calls during the same death silently no-op.</summary>
    public void Spawn(Vector2 foothold)
    {
        if (Started) return;
        Started = true;
        _world  = foothold;
        _fallFrames = LoadFrames("fall");
        _landFrames = LoadFrames("land");
        _burialSound = _soundWz?.GetItem("Game.img/Tombstone") as WzSound;
        _frameIndex = 0;
        _frameTimerMs = 0f;
        _fellDown = false;
        _soundPlayed = false;
        if (_fallFrames.Length == 0 && _landFrames.Length == 0)
        {
            _logger?.LogWarning("TombstoneEffect: no frames at Effect.wz/Tomb.img — degrading to no-op visual.");
            Land();   // still fire OnLanded so the revive dialog opens
        }
    }

    /// <summary>Hide and reset (called on field swap or revive completion).</summary>
    public void Reset()
    {
        Started = false;
        _fellDown = false;
        _frameIndex = 0;
        _frameTimerMs = 0f;
    }

    public void Update(float dt)
    {
        if (!Started) return;
        var ms = dt * 1000f;

        if (!_fellDown)
        {
            if (_fallFrames.Length == 0)
            {
                Land();
                return;
            }
            _frameTimerMs += ms;
            // Advance frame-by-frame using the per-frame WZ delays. One-shot: when
            // the last frame's delay elapses, switch to the landed state.
            while (_frameIndex < _fallFrames.Length - 1)
            {
                var d = _fallFrames[_frameIndex].DelayMs;
                if (_frameTimerMs < d) break;
                _frameTimerMs -= d;
                _frameIndex++;
            }
            if (_frameIndex >= _fallFrames.Length - 1 &&
                _frameTimerMs >= _fallFrames[^1].DelayMs)
            {
                Land();
            }
        }
        else if (_landFrames.Length > 1)
        {
            // Landed loop (most v95 landed sequences are a single frame; loop if
            // the asset is multi-frame).
            _frameTimerMs += ms;
            while (true)
            {
                var d = _landFrames[_frameIndex].DelayMs;
                if (_frameTimerMs < d) break;
                _frameTimerMs -= d;
                _frameIndex = (_frameIndex + 1) % _landFrames.Length;
            }
        }
    }

    private void Land()
    {
        _fellDown = true;
        _frameIndex = 0;
        _frameTimerMs = 0f;
        if (!_soundPlayed)
        {
            _soundPlayed = true;
            if (_burialSound is not null && _audio is not null)
            {
                _audio.PlayEffect(_burialSound);
            }
        }
        OnLanded?.Invoke();
    }

    /// <summary>Draw at the world-snapped landing point; <paramref name="worldToScreen"/>
    /// is the active camera's projector.</summary>
    public void Draw(SpriteBatch sb, Func<Vector2, Vector2> worldToScreen)
    {
        if (!Started) return;
        var frames = _fellDown ? _landFrames : _fallFrames;
        if (frames.Length == 0) return;
        var f = frames[Math.Min(_frameIndex, frames.Length - 1)];
        f.Sprite.Draw(sb, worldToScreen(_world), Color.White);
    }

    private FrameData[] LoadFrames(string kind)
    {
        if (_effectWz is null) return Array.Empty<FrameData>();
        var frames = new List<FrameData>();
        for (var i = 0; i < 64; i++)
        {
            var node = _effectWz.GetItem($"Tomb.img/{kind}/{i}");
            var canvas = node switch
            {
                WzCanvas c  => c,
                WzProperty p => p.Get("0") as WzCanvas ?? FindFirstCanvas(p),
                _            => null,
            };
            if (canvas is null) break;
            var sprite = _loader.Load(canvas);
            if (sprite is null) break;
            int delay = 100;
            if (node is WzProperty propNode &&
                propNode.Get("delay") is { } pd)
            {
                delay = pd switch { int v => v, short s => s, long l => (int)l, _ => 100 };
            }
            else if (canvas.Property.Get("delay") is { } cd)
            {
                delay = cd switch { int v => v, short s => s, long l => (int)l, _ => 100 };
            }
            if (delay <= 0) delay = 100;
            frames.Add(new FrameData { Sprite = sprite, DelayMs = delay });
        }
        // Single-canvas layout fallback: Tomb.img/<kind> directly carries the canvas.
        if (frames.Count == 0 &&
            _effectWz.GetItem($"Tomb.img/{kind}") is WzProperty rootProp &&
            FindFirstCanvas(rootProp) is { } singleCanvas)
        {
            var sprite = _loader.Load(singleCanvas);
            if (sprite is not null)
            {
                var d = rootProp.Get("delay") switch
                {
                    int v => v, short s => s, long l => (int)l, _ => 500,
                };
                if (d <= 0) d = 500;
                frames.Add(new FrameData { Sprite = sprite, DelayMs = d });
            }
        }
        return frames.ToArray();
    }

    private static WzCanvas? FindFirstCanvas(WzProperty p)
    {
        if (p.Get("0") is WzCanvas zero) return zero;
        foreach (var kv in p.Items)
        {
            if (kv.Value is WzCanvas c) return c;
        }
        return null;
    }

    private sealed class FrameData
    {
        public required WzSprite Sprite;
        public int DelayMs;
    }
}
