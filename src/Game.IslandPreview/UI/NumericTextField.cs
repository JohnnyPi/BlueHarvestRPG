using Microsoft.Xna.Framework;

namespace Game.IslandPreview.UI;

public sealed class NumericTextField
{
    private string _text = string.Empty;

    public bool IsFocused { get; private set; }
    public Rectangle Bounds { get; private set; }

    public void SetBounds(Rectangle bounds)
    {
        Bounds = bounds;
    }

    public void SetValue(string text)
    {
        _text = text;
    }

    public string GetText() => _text;

    public void Focus()
    {
        IsFocused = true;
    }

    public void Blur()
    {
        IsFocused = false;
    }

    public bool ContainsPoint(int x, int y)
    {
        return Bounds.Contains(x, y);
    }

    public void HandleTextInput(char character)
    {
        if (!IsFocused)
        {
            return;
        }

        if (char.IsControl(character))
        {
            return;
        }

        if (character is >= '0' and <= '9' or '.' or '-' or '+')
        {
            _text += character;
        }
    }

    public void HandleBackspace()
    {
        if (!IsFocused || _text.Length == 0)
        {
            return;
        }

        _text = _text[..^1];
    }

    public bool TryParseInt(out int value)
    {
        return int.TryParse(_text, out value);
    }

    public bool TryParseFloat(out float value)
    {
        return float.TryParse(_text, out value);
    }

    public bool TryParseULong(out ulong value)
    {
        return ulong.TryParse(_text, out value);
    }
}
