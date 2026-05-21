using MapleClaude.App;
using MapleClaude.Wz;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapleClaude.Stages;

/// <summary>
/// PIN entry stage. The upstream Kinoko server sets <c>bSkipPinCode = true</c>
/// on every successful login, so this stage is dormant in normal play. It
/// exists as a scaffold so we can wire <c>CheckPinCode(9)</c> /
/// <c>UpdatePinCode(10)</c> later (e.g. if the user runs a Kinoko build with
/// <c>ServerConfig.REQUIRE_PIN_CODE = true</c>).
/// </summary>
public sealed class PinStage : Stage
{
    private readonly ILogger<PinStage> _logger;
    private readonly WzPackage? _ui;

    public PinStage(ILogger<PinStage> logger, WzPackage? ui)
    {
        _logger = logger;
        _ui = ui;
    }

    public override void OnEnter(MapleClaudeGame game)
    {
        base.OnEnter(game);
        _ = _ui;
        _logger.LogInformation("PinStage entered (dormant — skipping immediately)");
        // If we ever wire PIN, send CheckPinCode(9): byte 1 + string sPin.
        // For now, just bounce to whatever the previous stage expected.
        // The caller normally bypasses pushing this stage entirely.
    }

    public override void Draw(GameTime gameTime, SpriteBatch sb)
    {
        var pp = GraphicsDevice.PresentationParameters;
        sb.Draw(Game.WhitePixel, pp.Bounds, new Color(0, 0, 0, 200));
        if (Game.Font is not null)
        {
            Game.Font.Draw(sb, "PIN entry — not yet exercised by this server.",
                new Vector2(40, 280), Color.White);
        }
    }
}
