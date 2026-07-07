using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.UI;

public sealed class UiPainter
{
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont? _font;

    public UiPainter(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont? font)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        _font = font;
    }

    public SpriteFont? Font => _font;

    public void DrawRect(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }

    public void DrawBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        DrawRect(x, y, width, thickness, color);
        DrawRect(x, y + height - thickness, width, thickness, color);
        DrawRect(x, y, thickness, height, color);
        DrawRect(x + width - thickness, y, thickness, height, color);
    }

    public void DrawString(string text, int x, int y, Color color)
    {
        if (_font is not null)
        {
            _spriteBatch.DrawString(_font, text, new Vector2(x, y), color);
        }
    }

    public Vector2 MeasureString(string text)
    {
        return _font?.MeasureString(text) ?? Vector2.Zero;
    }

    public void DrawHpBar(int x, int y, int width, int height, int current, int max, Color fill, Color background)
    {
        DrawRect(x, y, width, height, background);
        if (max > 0 && current > 0)
        {
            int fillWidth = Math.Max(1, (int)((long)width * current / max));
            DrawRect(x, y, fillWidth, height, fill);
        }
    }
}
