using Game.Content.Definitions;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class LandConnectivityStage
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        float landThreshold = config.LandElevationThreshold;
        int satelliteMinCells = ComputeSatelliteMinCells(plan, config);

        var components = FindLandComponents(plan);
        if (components.Count == 0)
        {
            return;
        }

        components.Sort((left, right) => right.Count.CompareTo(left.Count));
        var keep = new HashSet<(int X, int Y)>(components[0]);

        if (config.SatelliteIslandCount > 0)
        {
            int satellitesKept = 0;
            for (int i = 1; i < components.Count && satellitesKept < config.SatelliteIslandCount; i++)
            {
                if (components[i].Count < satelliteMinCells)
                {
                    continue;
                }

                foreach ((int x, int y) in components[i])
                {
                    keep.Add((x, y));
                }

                satellitesKept++;
            }
        }

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y) || keep.Contains((x, y)))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.IsLand = false;
                cell.IsCoast = false;
                cell.Role &= ~IslandCellRole.Coast;
                cell.Elevation = MathF.Min(cell.Elevation, landThreshold * 0.5f);
                cell.Biome = BiomeId.Ocean;
            }
        }

        LandmassStage.MarkCoastline(plan, config);
        SanitizeOceanCells(plan);
    }

    public static void SanitizeOceanCells(IslandPlan plan)
    {
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsLand)
                {
                    if (cell.Biome is BiomeId.Ocean or BiomeId.ShallowWater or BiomeId.Reef)
                    {
                        cell.Biome = cell.IsCoast ? BiomeId.Beach : BiomeId.Plains;
                    }

                    continue;
                }

                if (cell.Biome is BiomeId.ShallowWater or BiomeId.Reef)
                {
                    cell.IsCoast = false;
                    cell.Role &= ~IslandCellRole.Coast;
                    continue;
                }

                cell.Biome = BiomeId.Ocean;
                cell.IsCoast = false;
                cell.Role &= ~IslandCellRole.Coast;
            }
        }
    }

    private static int ComputeSatelliteMinCells(IslandPlan plan, IslandDefinition config)
    {
        float centerX = (plan.Width - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, (plan.Height - 1) * 0.5f);
        float minRadius = maxRadius * config.SatelliteMinRadius;
        return Math.Max(config.MinLandComponentCells, (int)(MathF.PI * minRadius * minRadius * 0.35f));
    }

    private static List<List<(int X, int Y)>> FindLandComponents(IslandPlan plan)
    {
        var visited = new bool[plan.Width * plan.Height];
        var components = new List<List<(int X, int Y)>>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int startIndex = y * plan.Width + x;
                if (visited[startIndex] || !plan.IsLand(x, y))
                {
                    continue;
                }

                var component = new List<(int X, int Y)>();
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[startIndex] = true;

                while (queue.Count > 0)
                {
                    (int cx, int cy) = queue.Dequeue();
                    component.Add((cx, cy));

                    foreach ((int dx, int dy) in Neighbors)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (!plan.Contains(nx, ny))
                        {
                            continue;
                        }

                        int neighborIndex = ny * plan.Width + nx;
                        if (visited[neighborIndex] || !plan.IsLand(nx, ny))
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                components.Add(component);
            }
        }

        return components;
    }
}
