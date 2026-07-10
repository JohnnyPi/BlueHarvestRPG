using Game.Content.Definitions;
using Game.Generation.Island.Fields;
using Game.Generation.Noise;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
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
                float minimum = cell.IsLand ? landThreshold + 0.001f : 0f;
                cell.Elevation = Math.Clamp(cell.Elevation + deltas[i], minimum, 1.25f);
            }
        }

        BuildRiverGraph(plan, config, stageSeed);
        AccumulateDrainage(plan);
        DerivedFieldsStage.ComputeRiverInfluence(plan);

        if (!config.UseLegacyIslandMask)
        {
            CoastlineVariationStage.CarveRiverMouthInlets(plan, config, stageSeed + 200);
        }
    }

    private static void BuildRiverGraph(IslandPlan plan, IslandDefinition config, ulong stageSeed)
    {
        plan.RiverGraph.Segments.Clear();
        plan.RiverGraph.PathCells.Clear();
        plan.RiverGraph.GlobalRiverTiles.Clear();

        if (config.RiverCount <= 0 || config.RiverCarveDepth <= 0f)
        {
            return;
        }

        int count = plan.Width * plan.Height;
        if (plan.IsRiverCell.Length != count)
        {
            plan.IsRiverCell = new bool[count];
        }
        else
        {
            Array.Clear(plan.IsRiverCell);
        }

        var sources = new List<(int X, int Y, float Score)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand
                    || cell.IsCoast
                    || cell.Elevation < config.RiverMinElevation
                    || plan.VolcanoExclusion.IsProtected(x, y)
                    || plan.LavaFlowGraph.PathCells.Contains((x, y)))
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
        var carved = new HashSet<(int X, int Y)>();
        var acceptedHeads = new List<(int X, int Y)>();
        HashSet<(int X, int Y)> exteriorWater = FindExteriorWater(plan);
        int riversPlaced = 0;

        for (int i = 0; i < sources.Count && riversPlaced < config.RiverCount; i++)
        {
            (int x, int y, _) = sources[i];
            if (acceptedHeads.Any(head =>
                    Square(head.X - x) + Square(head.Y - y)
                    < config.RiverHeadSpacing * config.RiverHeadSpacing))
            {
                continue;
            }

            if (!carved.Add((x, y)))
            {
                continue;
            }

            List<WorldCoord>? path = TraceAndCarve(plan, config, stageSeed, x, y, carved, exteriorWater);
            if (path is null || !exteriorWater.Contains((path[^1].X, path[^1].Y)))
            {
                continue;
            }

            var segment = new FacilityRiverSegment();
            segment.Path.AddRange(path);
            plan.RiverGraph.Segments.Add(segment);
            plan.RiverGraph.AddPath(path);
            GlobalTilePathUtility.AddPathWithBorderRuns(plan.RiverGraph.GlobalRiverTiles, path, config.RiverWidth);
            acceptedHeads.Add((x, y));
            riversPlaced++;
        }
    }

    public static void ReconcileRiverMouths(IslandPlan plan, IslandDefinition config)
    {
        if (plan.RiverGraph.Segments.Count == 0
            || plan.ExteriorOcean.Length != plan.Width * plan.Height)
        {
            return;
        }

        var exteriorWater = new HashSet<(int X, int Y)>();
        for (int index = 0; index < plan.ExteriorOcean.Length; index++)
        {
            if (plan.ExteriorOcean[index])
            {
                exteriorWater.Add((index % plan.Width, index / plan.Width));
            }
        }

        for (int i = plan.RiverGraph.Segments.Count - 1; i >= 0; i--)
        {
            FacilityRiverSegment segment = plan.RiverGraph.Segments[i];
            WorldCoord mouth = segment.Path[^1];
            if (exteriorWater.Contains((mouth.X, mouth.Y)))
            {
                continue;
            }

            int x = mouth.X;
            int y = mouth.Y;
            if (!TryExtendToOcean(plan, exteriorWater, ref x, ref y, segment.Path))
            {
                plan.RiverGraph.Segments.RemoveAt(i);
            }
        }

        plan.RiverGraph.PathCells.Clear();
        plan.RiverGraph.GlobalRiverTiles.Clear();
        foreach (FacilityRiverSegment segment in plan.RiverGraph.Segments)
        {
            plan.RiverGraph.AddPath(segment.Path);
            foreach (WorldCoord cell in segment.Path)
            {
                if (plan.Contains(cell.X, cell.Y))
                {
                    plan.IsRiverCell[cell.Y * plan.Width + cell.X] = true;
                }
            }

            GlobalTilePathUtility.AddPathWithBorderRuns(
                plan.RiverGraph.GlobalRiverTiles,
                segment.Path,
                config.RiverWidth);
        }
    }

    private static HashSet<(int X, int Y)> FindExteriorWater(IslandPlan plan)
    {
        var exterior = new HashSet<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();

        void AddIfWater(int x, int y)
        {
            if (!plan.IsLand(x, y) && exterior.Add((x, y)))
            {
                queue.Enqueue((x, y));
            }
        }

        for (int x = 0; x < plan.Width; x++)
        {
            AddIfWater(x, 0);
            AddIfWater(x, plan.Height - 1);
        }

        for (int y = 1; y < plan.Height - 1; y++)
        {
            AddIfWater(0, y);
            AddIfWater(plan.Width - 1, y);
        }

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (plan.Contains(nx, ny) && !plan.IsLand(nx, ny) && exterior.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return exterior;
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

    private static List<WorldCoord>? TraceAndCarve(
        IslandPlan plan,
        IslandDefinition config,
        ulong stageSeed,
        int startX,
        int startY,
        HashSet<(int X, int Y)> carved,
        HashSet<(int X, int Y)> exteriorWater)
    {
        int x = startX;
        int y = startY;
        int previousX = startX;
        int previousY = startY;
        int steps = 0;
        int maxSteps = config.RiverMaxLength;
        var path = new List<WorldCoord>();

        while (steps < maxSteps && plan.Contains(x, y))
        {
            ref IslandCellData cell = ref plan.GetCell(x, y);
            path.Add(new WorldCoord(x, y));

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
            cell.Elevation = MathF.Max(
                config.LandElevationThreshold + 0.001f,
                cell.Elevation - carve - jitter);
            plan.IsRiverCell[y * plan.Width + x] = true;

            if (cell.IsCoast)
            {
                if (!TryExtendToOcean(plan, exteriorWater, ref x, ref y, path))
                {
                    return null;
                }

                break;
            }

            if (!TryFindDownhillNeighbor(
                    plan,
                    x,
                    y,
                    previousX,
                    previousY,
                    out int nextX,
                    out int nextY))
            {
                if (!TryExtendToOcean(plan, exteriorWater, ref x, ref y, path))
                {
                    return null;
                }

                break;
            }

            if (!carved.Add((nextX, nextY)))
            {
                break;
            }

            previousX = x;
            previousY = y;
            x = nextX;
            y = nextY;
            steps++;
        }

        return path.Count > 1 && exteriorWater.Contains((path[^1].X, path[^1].Y)) ? path : null;
    }

    private static bool TryExtendToOcean(
        IslandPlan plan,
        HashSet<(int X, int Y)> exteriorWater,
        ref int x,
        ref int y,
        List<WorldCoord> path)
    {
        var parent = new Dictionary<(int X, int Y), (int X, int Y)>();
        var cost = new Dictionary<(int X, int Y), float> { [(x, y)] = 0f };
        var queue = new PriorityQueue<(int X, int Y), float>();
        queue.Enqueue((x, y), 0f);

        while (queue.Count > 0)
        {
            (int cx, int cy) = queue.Dequeue();
            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!plan.Contains(nx, ny))
                {
                    continue;
                }

                ref IslandCellData neighbor = ref plan.GetCell(nx, ny);
                if (!exteriorWater.Contains((nx, ny))
                    && (plan.VolcanoExclusion.IsProtected(nx, ny)
                        || plan.LavaFlowGraph.PathCells.Contains((nx, ny))))
                {
                    continue;
                }

                float stepCost = 1f;
                if (neighbor.IsLand)
                {
                    float climb = MathF.Max(0f, neighbor.Elevation - plan.GetCell(cx, cy).Elevation);
                    stepCost += climb * 40f + MathF.Max(0f, neighbor.Elevation) * 0.35f;
                }

                float candidateCost = cost[(cx, cy)] + stepCost;
                if (cost.TryGetValue((nx, ny), out float knownCost) && candidateCost >= knownCost)
                {
                    continue;
                }

                cost[(nx, ny)] = candidateCost;
                parent[(nx, ny)] = (cx, cy);
                if (exteriorWater.Contains((nx, ny)))
                {
                    var extension = new List<(int X, int Y)>();
                    (int X, int Y) cursor = (nx, ny);
                    while (cursor != (x, y))
                    {
                        extension.Add(cursor);
                        cursor = parent[cursor];
                    }

                    extension.Reverse();
                    foreach ((int pathX, int pathY) in extension)
                    {
                        path.Add(new WorldCoord(pathX, pathY));
                        plan.IsRiverCell[pathY * plan.Width + pathX] = true;
                    }

                    x = nx;
                    y = ny;
                    return true;
                }

                if (neighbor.IsCoast || neighbor.IsLand)
                {
                    queue.Enqueue((nx, ny), candidateCost);
                }
            }
        }

        return false;
    }

    private static bool TryFindDownhillNeighbor(
        IslandPlan plan,
        int x,
        int y,
        int previousX,
        int previousY,
        out int nextX,
        out int nextY)
    {
        ref IslandCellData cell = ref plan.GetCell(x, y);
        nextX = x;
        nextY = y;
        float bestScore = cell.Elevation;
        bool found = false;

        foreach ((int dx, int dy) in Neighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!plan.Contains(nx, ny))
            {
                continue;
            }

            if (plan.VolcanoExclusion.IsProtected(nx, ny)
                || plan.LavaFlowGraph.PathCells.Contains((nx, ny)))
            {
                continue;
            }

            float elevation = plan.GetCell(nx, ny).Elevation;
            if (elevation >= cell.Elevation)
            {
                continue;
            }

            int incomingX = Math.Sign(x - previousX);
            int incomingY = Math.Sign(y - previousY);
            int outgoingX = Math.Sign(nx - x);
            int outgoingY = Math.Sign(ny - y);
            float turnPenalty = (incomingX != 0 || incomingY != 0)
                && (incomingX != outgoingX || incomingY != outgoingY)
                    ? 0.008f
                    : 0f;
            float score = elevation + turnPenalty;
            if (score < bestScore)
            {
                bestScore = score;
                nextX = nx;
                nextY = ny;
                found = true;
            }
        }

        return found;
    }

    private static int Square(int value) => value * value;
}
