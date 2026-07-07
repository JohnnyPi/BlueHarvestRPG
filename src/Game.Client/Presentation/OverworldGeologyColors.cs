using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

public static class OverworldGeologyColors
{
    public static readonly Color River = new(0x4F, 0xC3, 0xF7, 220);
    public static readonly Color Divergent = new(0x2E, 0xCC, 0xB0, 230);
    public static readonly Color Subduction = new(0xE7, 0x4C, 0x3C, 230);
    public static readonly Color Collision = new(0xF3, 0x9C, 0x12, 230);
    public static readonly Color Transform = new(0xF1, 0xC4, 0x0F, 230);

    public static Color? ForBoundaryType(byte boundaryType)
    {
        return boundaryType switch
        {
            1 => Divergent,
            2 => Subduction,
            3 => Collision,
            4 => Transform,
            _ => null
        };
    }

    public static string LabelForBoundaryType(byte boundaryType)
    {
        return boundaryType switch
        {
            1 => "Divergent rift",
            2 => "Subduction zone",
            3 => "Collision front",
            4 => "Transform fault",
            _ => string.Empty
        };
    }
}
