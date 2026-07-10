using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

internal static class IslandPathfinder
{
    private static readonly (int Dx, int Dy)[] Neighbors =
    [
        (0, -1),
        (1, -1),
        (1, 0),
        (1, 1),
        (0, 1),
        (-1, 1),
        (-1, 0),
        (-1, -1),
    ];

    public static List<WorldCoord>? FindPath(IslandPlan plan, WorldCoord start, WorldCoord goal)
    {
        if (start == goal)
        {
            return [start];
        }

        if (plan.VolcanoExclusion.IsProtected(start.X, start.Y)
            || plan.VolcanoExclusion.IsProtected(goal.X, goal.Y))
        {
            return null;
        }

        var open = new PriorityQueue<WorldCoord, float>();
        var closed = new HashSet<WorldCoord>();
        var cameFrom = new Dictionary<WorldCoord, WorldCoord>();
        var gScore = new Dictionary<WorldCoord, float> { [start] = 0f };
        open.Enqueue(start, Heuristic(start, goal));

        while (open.Count > 0)
        {
            WorldCoord current = open.Dequeue();
            if (!closed.Add(current))
            {
                continue;
            }

            if (current == goal)
            {
                return Reconstruct(plan, cameFrom, current);
            }

            float currentScore = gScore[current];
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
                if (closed.Contains(neighbor) || plan.VolcanoExclusion.IsProtected(nx, ny))
                {
                    continue;
                }

                if (dx != 0 && dy != 0
                    && !IsTraversable(plan, current.X + dx, current.Y)
                    && !IsTraversable(plan, current.X, current.Y + dy))
                {
                    continue;
                }

                float stepCost = GetMoveCost(plan, cameFrom, current, neighbor, cell.Biome);
                float tentative = currentScore + stepCost;
                if (gScore.TryGetValue(neighbor, out float known) && tentative >= known)
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

    private static float Heuristic(WorldCoord a, WorldCoord b)
    {
        int dx = Math.Abs(a.X - b.X);
        int dy = Math.Abs(a.Y - b.Y);
        int diagonal = Math.Min(dx, dy);
        int straight = Math.Max(dx, dy) - diagonal;
        return diagonal * 1.41421356f + straight;
    }

    private static float GetMoveCost(
        IslandPlan plan,
        IReadOnlyDictionary<WorldCoord, WorldCoord> cameFrom,
        WorldCoord current,
        WorldCoord neighbor,
        BiomeId biome)
    {
        float biomeCost = biome switch
        {
            BiomeId.Plains => 1f,
            BiomeId.Forest => 2.2f,
            BiomeId.Beach => 2.5f,
            BiomeId.Hills => 3.5f,
            BiomeId.Jungle => 4.5f,
            BiomeId.Swamp => 7f,
            BiomeId.Volcanic => 12f,
            BiomeId.Mountains => 24f,
            _ => 10f
        };

        float elevationDelta = MathF.Abs(
            plan.GetCell(current.X, current.Y).Elevation -
            plan.GetCell(neighbor.X, neighbor.Y).Elevation);
        int index = neighbor.Y * plan.Width + neighbor.X;
        float slope = plan.Slope.Length == plan.Width * plan.Height
            ? plan.Slope[index]
            : elevationDelta;
        float lavaPenalty = plan.LavaFlowGraph.PathCells.Contains((neighbor.X, neighbor.Y))
            ? plan.LavaFlowGraph.RoadTraversalPenalty
            : 0f;
        float volcanoBoundaryDistance = plan.VolcanoExclusion.DistanceToNearestBoundary(neighbor.X, neighbor.Y);
        float volcanoPenalty = float.IsFinite(volcanoBoundaryDistance)
            ? MathF.Max(0f, 0.65f - volcanoBoundaryDistance) * 18f
            : 0f;
        float dx = neighbor.X - current.X;
        float dy = neighbor.Y - current.Y;
        float diagonalCost = dx != 0f && dy != 0f ? 1.41421356f : 1f;
        float turnPenalty = 0f;
        if (cameFrom.TryGetValue(current, out WorldCoord previous))
        {
            int previousDx = Math.Sign(current.X - previous.X);
            int previousDy = Math.Sign(current.Y - previous.Y);
            if (previousDx != Math.Sign(dx) || previousDy != Math.Sign(dy))
            {
                turnPenalty = 0.45f;
            }
        }

        float variation = CoordinateVariation(plan.Seed, neighbor.X, neighbor.Y);
        return diagonalCost * biomeCost
            + elevationDelta * 45f
            + slope * 18f
            + lavaPenalty
            + volcanoPenalty
            + turnPenalty
            + variation;
    }

    private static float CoordinateVariation(ulong seed, int x, int y)
    {
        ulong value = seed ^ ((ulong)(uint)x * 0x9E3779B185EBCA87UL) ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL);
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        return (value & 0xFFFFUL) / 65535f * 0.35f;
    }

    private static List<WorldCoord> Reconstruct(
        IslandPlan plan,
        Dictionary<WorldCoord, WorldCoord> cameFrom,
        WorldCoord current)
    {
        var path = new List<WorldCoord> { current };
        while (cameFrom.TryGetValue(current, out WorldCoord previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        var cardinalPath = new List<WorldCoord> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            WorldCoord previous = cardinalPath[^1];
            WorldCoord next = path[i];
            if (previous.X != next.X && previous.Y != next.Y)
            {
                var horizontalFirst = new WorldCoord(next.X, previous.Y);
                var verticalFirst = new WorldCoord(previous.X, next.Y);
                bool horizontalValid = IsTraversable(plan, horizontalFirst.X, horizontalFirst.Y);
                bool verticalValid = IsTraversable(plan, verticalFirst.X, verticalFirst.Y);
                WorldCoord bridge = horizontalValid && verticalValid
                    ? CoordinateVariation(plan.Seed, next.X, next.Y) < 0.175f
                        ? horizontalFirst
                        : verticalFirst
                    : horizontalValid
                        ? horizontalFirst
                        : verticalFirst;
                cardinalPath.Add(bridge);
            }

            cardinalPath.Add(next);
        }

        return cardinalPath;
    }

    private static bool IsTraversable(IslandPlan plan, int x, int y)
        => plan.Contains(x, y)
            && plan.IsLand(x, y)
            && !plan.VolcanoExclusion.IsProtected(x, y);
}
