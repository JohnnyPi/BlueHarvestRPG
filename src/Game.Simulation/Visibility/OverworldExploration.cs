using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Simulation.Visibility;

public static class OverworldExploration
{
    public const int RevealRadius = 4;

    public static void InitializeTouristMap(Overworld overworld)
    {
        WorldCoord center = overworld.IslandPlan?.VisitorCenterCell is { X: >= 0 } visitor
            ? visitor
            : new WorldCoord(overworld.Width / 2, overworld.Height / 2);

        RevealAround(overworld, center, RevealRadius);
    }

    public static void ComputeVisible(Overworld overworld, WorldCoord center, bool[] visible)
    {
        Array.Clear(visible, 0, visible.Length);

        for (int dy = -RevealRadius; dy <= RevealRadius; dy++)
        {
            for (int dx = -RevealRadius; dx <= RevealRadius; dx++)
            {
                if (dx * dx + dy * dy > RevealRadius * RevealRadius)
                {
                    continue;
                }

                var coord = new WorldCoord(center.X + dx, center.Y + dy);
                if (!overworld.Contains(coord))
                {
                    continue;
                }

                visible[overworld.GetIndex(coord)] = true;
            }
        }
    }

    public static void RevealAround(Overworld overworld, WorldCoord center, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                var coord = new WorldCoord(center.X + dx, center.Y + dy);
                if (!overworld.Contains(coord))
                {
                    continue;
                }

                overworld.Explored[overworld.GetIndex(coord)] = true;
            }
        }
    }

    public static void RevealAll(Overworld overworld)
    {
        Array.Fill(overworld.Explored, true);
    }
}
