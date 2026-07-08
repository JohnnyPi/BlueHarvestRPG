using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

internal static class CellVisibilityTint
{
    public static Color Resolve(
        RenderSnapshot snapshot,
        int x,
        int y,
        int index,
        Color baseTint,
        Color unseenColor,
        Color exploredDimColor,
        Color hazardTravelColor)
    {
        if (snapshot.VisibleTiles is not null &&
            snapshot.ExploredTiles is not null)
        {
            if (!snapshot.ExploredTiles[index])
            {
                return unseenColor;
            }

            if (!snapshot.VisibleTiles[index])
            {
                return Color.Lerp(baseTint, exploredDimColor, 0.65f);
            }
        }
        else if (snapshot.ViewMode == GameViewMode.Overworld &&
                 snapshot.ExploredTiles is not null &&
                 !snapshot.ExploredTiles[index])
        {
            return unseenColor;
        }
        else if (snapshot.ViewMode == GameViewMode.Overworld &&
                 snapshot.HazardousTravelX == x &&
                 snapshot.HazardousTravelY == y)
        {
            return Color.Lerp(baseTint, hazardTravelColor, 0.55f);
        }

        return baseTint;
    }

    public static bool IsUnseen(RenderSnapshot snapshot, int index)
    {
        return snapshot.ExploredTiles is not null && !snapshot.ExploredTiles[index];
    }

    public static Color ApplyFog(RenderSnapshot snapshot, int index, Color color, Color exploredDimColor)
    {
        if (snapshot.VisibleTiles is not null &&
            snapshot.ExploredTiles is not null &&
            snapshot.ExploredTiles[index] &&
            !snapshot.VisibleTiles[index])
        {
            return Color.Lerp(color, exploredDimColor, 0.65f);
        }

        return color;
    }
}
