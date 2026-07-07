using Game.Simulation.Coordinates;

namespace Game.Simulation.World;

public static class OverworldTravelCost
{
    public const int RiverCrossingCost = 30;

    public static int GetStepCost(Overworld overworld, WorldCoord from, WorldCoord to)
    {
        int cost = BiomeTraversal.GetMoveCost(overworld.GetCellValue(to).Biome);
        if (HasRiverCrossing(overworld, from, to))
        {
            cost += RiverCrossingCost;
        }

        return cost;
    }

    public static bool HasRiverCrossing(Overworld overworld, WorldCoord from, WorldCoord to)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1)
        {
            return false;
        }

        ConnectionFlags riverFlag = dx switch
        {
            1 => ConnectionFlags.EastRiver,
            -1 => ConnectionFlags.WestRiver,
            _ when dy == -1 => ConnectionFlags.NorthRiver,
            _ when dy == 1 => ConnectionFlags.SouthRiver,
            _ => ConnectionFlags.None
        };

        if (riverFlag == ConnectionFlags.None)
        {
            return false;
        }

        return (overworld.GetCellValue(from).ConnectionFlags & riverFlag) != 0;
    }
}
