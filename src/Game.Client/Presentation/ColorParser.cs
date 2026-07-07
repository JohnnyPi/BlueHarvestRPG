using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

public static class ColorParser
{
    public static Color Parse(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        string value = hex.Trim().TrimStart('#');
        if (value.Length != 6)
        {
            return fallback;
        }

        if (byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return new Color(r, g, b);
        }

        return fallback;
    }
}
