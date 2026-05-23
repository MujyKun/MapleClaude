using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MapleClaude.UI.Game;

public abstract class GamePanel
{
    public bool IsVisible { get; set; }
    public Vector2 Position { get; set; }

    /// <summary>
    /// Reposition for the active viewport. Called when the in-game stage loads
    /// and whenever the window resolution changes (e.g. login 800×600 → in-game
    /// 1024×768). The default is a no-op so panels keep their authored position;
    /// edge-anchored HUD panels override this to re-anchor to a screen edge.
    /// </summary>
    public virtual void Relayout(int viewWidth, int viewHeight) { }

    public virtual void Update(GameTime gameTime) { }
    public virtual void Draw(SpriteBatch spriteBatch, Texture2D white) { }
    public virtual bool HandleMouseButton(int x, int y, bool down) => false;
    public virtual bool OnKeyPress(Keys key) => false;
    public virtual void OnTextInput(char character) { }
}
