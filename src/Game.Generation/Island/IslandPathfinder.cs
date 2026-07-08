using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

internal static class IslandPathfinder
{
    private static readonly (int Dx, int Dy)[] Neighbors =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
    ];

    public static List<WorldCoord>? FindPath(IslandPlan plan, WorldCoord start, WorldCoord goal)
    {
        if (start == goal)
        {
            return [start];
        }

        var open = new PriorityQueue<WorldCoord, int>();
        var cameFrom = new Dictionary<WorldCoord, WorldCoord>();
        var gScore = new Dictionary<WorldCoord, int> { [start] = 0 };
        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            WorldCoord current = open.Dequeue();
            if (current == goal)
            {
                return Reconstruct(cameFrom, current);
            }

            int currentScore = gScore[current];
            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = current.X + dx;
                int ny = current.Y + dy;
                if (!plan.Contains(nx, ny))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(nx, ny);
                if (!cell.IsLand)
                {
                    continue;
                }

                var neighbor = new WorldCoord(nx, ny);
                int stepCost = GetMoveCost(cell.Biome);
                int tentative = currentScore + stepCost;
                if (gScore.TryGetValue(neighbor, out int known) && tentative >= known)
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentative;
                open.Enqueue(neighbor, tentative + Heuristic(neighbor, goal));
            }
        }

        return null;
    }

    private static int Heuristic(WorldCoord a, WorldCoord b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    private static int GetMoveCost(BiomeId biome)
    {
        return biome switch
        {
            BiomeId.Plains => 1,
            BiomeId.Forest => 2,
            BiomeId.Beach => 2,
            BiomeId.Hills => 3,
            BiomeId.Jungle => 4,
            BiomeId.Swamp => 6,
            BiomeId.Volcanic => 8,
            BiomeId.Mountains => 20,
            _ => 10
        };
    }

    private static List<WorldCoord> Reconstruct(Dictionary<WorldCoord, WorldCoord> cameFrom, WorldCoord current)
    {
        var path = new List<WorldCoord> { current };
        while (cameFrom.TryGetValue(current, out WorldCoord previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
