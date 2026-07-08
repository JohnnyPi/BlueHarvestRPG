using Game.Content.Definitions;
using Game.Generation.Island.Fields;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class ErosionStage
{
    private const uint StageSalt = 15;
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        if (config.ErosionIterations <= 0)
        {
            return;
        }

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        float landThreshold = config.LandElevationThreshold;

        for (int iteration = 0; iteration < config.ErosionIterations; iteration++)
        {
            var deltas = new float[plan.Width * plan.Height];

            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    ref IslandCellData cell = ref plan.GetCell(x, y);
                    if (!cell.IsLand || cell.Elevation <= landThreshold)
                    {
                        continue;
                    }

                    float lowest = cell.Elevation;
                    int lowestX = x;
                    int lowestY = y;

                    foreach ((int dx, int dy) in Neighbors)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (!plan.Contains(nx, ny))
                        {
                            continue;
                        }

                        float neighborElevation = plan.GetCell(nx, ny).Elevation;
                        if (neighborElevation < lowest)
                        {
                            lowest = neighborElevation;
                            lowestX = nx;
                            lowestY = ny;
                        }
                    }

                    if (lowestX == x && lowestY == y)
                    {
                        continue;
                    }

                    float slope = cell.Elevation - lowest;
                    if (slope <= config.ErosionStrength)
                    {
                        continue;
                    }

                    float transfer = MathF.Min(config.ErosionStrength, slope * 0.5f);
                    int index = y * plan.Width + x;
                    int depositIndex = lowestY * plan.Width + lowestX;
                    deltas[index] -= transfer;
                    if (plan.IsLand(lowestX, lowestY))
                    {
                        deltas[depositIndex] += transfer * 0.85f;
                    }
                }
            }

            for (int i = 0; i < deltas.Length; i++)
            {
                if (MathF.Abs(deltas[i]) <= 0f)
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.Cells[i];
                cell.Elevation = Math.Clamp(cell.Elevation + deltas[i], 0f, 1.25f);
            }
        }

        CarveRiverPaths(plan, config, stageSeed);
        AccumulateDrainage(plan);
        DerivedFieldsStage.ComputeRiverInfluence(plan);
    }

    private static void AccumulateDrainage(IslandPlan plan)
    {
        int count = plan.Width * plan.Height;
        plan.Drainage = new float[count];

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                float accumulation = 1f;
                if (plan.IsRiverCell.Length > index && plan.IsRiverCell[index])
                {
                    accumulation += 4f;
                }

                foreach ((int dx, int dy) in Neighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!plan.Contains(nx, ny) || !plan.IsLand(nx, ny))
                    {
                        continue;
                    }

                    if (plan.GetCell(nx, ny).Elevation >= plan.GetCell(x, y).Elevation)
                    {
                        accumulation += 0.25f;
                    }
                }

                plan.Drainage[index] = accumulation;
            }
        }
    }

    private static void CarveRiverPaths(IslandPlan plan, IslandDefinition config, ulong stageSeed)
    {
        if (config.RiverCount <= 0 || config.RiverCarveDepth <= 0f)
        {
            return;
        }

        int count = plan.Width * plan.Height;
        if (plan.IsRiverCell.Length != count)
        {
            plan.IsRiverCell = new bool[count];
        }

        var sources = new List<(int X, int Y, float Score)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast || cell.Elevation < config.RiverMinElevation)
                {
                    continue;
                }

                float score = cell.Elevation * 2f + cell.Moisture * 0.4f;
                if (!config.UseLegacyIslandMask)
                {
                    score += RidgeSplineField.SampleAtCell(x, y, plan.Width, plan.Height, config.Ridges) * 1.5f;
                }

                sources.Add((x, y, score));
            }
        }

        sources.Sort((a, b) => b.Score.CompareTo(a.Score));
        int maxSources = Math.Min(config.RiverCount * 2, sources.Count);
        var carved = new HashSet<(int X, int Y)>();

        for (int i = 0; i < maxSources; i++)
        {
            (int x, int y, _) = sources[i];
            if (!carved.Add((x, y)))
            {
                continue;
            }

            TraceAndCarve(plan, config, stageSeed, x, y, carved);
        }
    }

    private static void TraceAndCarve(
        IslandPlan plan,
        IslandDefinition config,
        ulong stageSeed,
        int startX,
        int startY,
        HashSet<(int X, int Y)> carved)
    {
        int x = startX;
        int y = startY;
        int steps = 0;
        int maxSteps = config.RiverMaxLength;

        while (steps < maxSteps && plan.Contains(x, y))
        {
            ref IslandCellData cell = ref plan.GetCell(x, y);
            if (!cell.IsLand)
            {
                break;
            }

            float carve = config.RiverCarveDepth;
            if (cell.IsCoast)
            {
                carve *= 1.5f;
            }

            float jitter = NoiseUtility.Fbm(stageSeed + 20, x * 0.07f, y * 0.07f, octaves: 2) * 0.01f;
            cell.Elevation = MathF.Max(config.LandElevationThreshold * 0.5f, cell.Elevation - carve - jitter);
            plan.IsRiverCell[y * plan.Width + x] = true;

            if (cell.IsCoast)
            {
                break;
            }

            int nextX = x;
            int nextY = y;
            float lowest = cell.Elevation;

            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.Contains(nx, ny))
                {
                    continue;
                }

                float elevation = plan.GetCell(nx, ny).Elevation;
                if (elevation < lowest)
                {
                    lowest = elevation;
                    nextX = nx;
                    nextY = ny;
                }
            }

            if (nextX == x && nextY == y)
            {
                break;
            }

            if (!carved.Add((nextX, nextY)))
            {
                break;
            }

            x = nextX;
            y = nextY;
            steps++;
        }
    }
}
