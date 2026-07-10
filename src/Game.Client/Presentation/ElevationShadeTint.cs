using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

internal static class ElevationShadeTint
{
    public static Color Apply(Color color, float brightness)
    {
        brightness = Math.Clamp(brightness, 0f, 1f);
        return new Color(
            (byte)Math.Round(color.R * brightness),
            (byte)Math.Round(color.G * brightness),
            (byte)Math.Round(color.B * brightness),
            color.A);
    }
}
