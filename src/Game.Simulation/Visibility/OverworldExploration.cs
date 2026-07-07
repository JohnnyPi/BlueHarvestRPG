using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Visibility;

public static class OverworldExploration
{
    public const int RevealRadius = 4;

    public static void InitializeTouristMap(Overworld overworld)
    {
        if (overworld.IslandPlan is null)
        {
            RevealAround(overworld, new WorldCoord(overworld.Width / 2, overworld.Height / 2), RevealRadius);
            return;
        }

        IslandPlan plan = overworld.IslandPlan;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (plan.GetCell(x, y).IsCoast)
                {
                    overworld.Explored[overworld.GetIndex(new WorldCoord(x, y))] = true;
                }
            }
        }

        if (plan.VisitorCenterCell.X >= 0)
        {
            RevealAround(overworld, plan.VisitorCenterCell, RevealRadius);
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
}
