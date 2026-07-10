using Game.Content.Definitions;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class CoastlineCleanupStage
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(
        IslandPlan plan,
        IslandDefinition config,
        bool recomputeCoastDistance = true)
    {
        if (config.UseLegacyIslandMask)
        {
            return;
        }

        float landThreshold = config.IslandShape.LandThreshold;
        int minComponentCells = Math.Max(1, config.MinLandComponentCells / 3);
        SmoothSingleCellNoise(plan, landThreshold);
        RemoveTinyMaskIslands(plan, landThreshold, minComponentCells);
        if (recomputeCoastDistance)
        {
            CoastDistanceStage.Execute(plan, config);
        }
    }

    private static void SmoothSingleCellNoise(IslandPlan plan, float landThreshold)
    {
        var smoothed = new float[plan.IslandMask.Length];
        Array.Copy(plan.IslandMask, smoothed, plan.IslandMask.Length);

        for (int y = 1; y < plan.Height - 1; y++)
        {
            for (int x = 1; x < plan.Width - 1; x++)
            {
                int index = y * plan.Width + x;
                bool isLand = plan.IslandMask[index] > landThreshold;
                int landNeighbors = 0;
                int oceanNeighbors = 0;

                foreach ((int dx, int dy) in Neighbors)
                {
                    bool neighborLand = plan.IslandMask[(y + dy) * plan.Width + (x + dx)] > landThreshold;
                    if (neighborLand)
                    {
                        landNeighbors++;
                    }
                    else
                    {
                        oceanNeighbors++;
                    }
                }

                if (isLand && oceanNeighbors >= 3)
                {
                    smoothed[index] = landThreshold * 0.5f;
                }
                else if (!isLand && landNeighbors >= 3)
                {
                    smoothed[index] = landThreshold * 1.5f;
                }
            }
        }

        plan.IslandMask = smoothed;
    }

    private static void RemoveTinyMaskIslands(IslandPlan plan, float landThreshold, int minCells)
    {
        var visited = new bool[plan.Width * plan.Height];
        var components = new List<List<int>>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int start = y * plan.Width + x;
                if (visited[start] || plan.IslandMask[start] <= landThreshold)
                {
                    continue;
                }

                var component = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(start);
                visited[start] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    component.Add(current);
                    int cx = current % plan.Width;
                    int cy = current / plan.Width;

                    foreach ((int dx, int dy) in Neighbors)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
                        {
                            continue;
                        }

                        int neighbor = ny * plan.Width + nx;
                        if (visited[neighbor] || plan.IslandMask[neighbor] <= landThreshold)
                        {
                            continue;
                        }

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                components.Add(component);
            }
        }

        if (components.Count <= 1)
        {
            return;
        }

        components.Sort((left, right) => right.Count.CompareTo(left.Count));
        var keep = new HashSet<int>(components[0]);

        for (int i = 1; i < components.Count; i++)
        {
            if (components[i].Count >= minCells)
            {
                foreach (int index in components[i])
                {
                    keep.Add(index);
                }
            }
        }

        for (int i = 0; i < plan.IslandMask.Length; i++)
        {
            if (plan.IslandMask[i] > landThreshold && !keep.Contains(i))
            {
                plan.IslandMask[i] = landThreshold * 0.25f;
            }
        }
    }
}
