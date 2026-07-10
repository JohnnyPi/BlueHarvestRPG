using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Breaks up straight coastlines with coastal cellular automata and carves procedural inlets.
/// </summary>
public static class CoastlineVariationStage
{
    private const uint StageSalt = 18;

    private static readonly (int Dx, int Dy)[] CardinalNeighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int Dx, int Dy)[] MooreNeighbors =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1)
    ];

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        if (config.UseLegacyIslandMask)
        {
            return;
        }

        float landThreshold = config.IslandShape.LandThreshold;
        IslandCoastlineDetailDefinition detail = config.IslandShape.CoastlineDetail;
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        ApplyCoastalCellularAutomata(plan, landThreshold, detail, stageSeed);
        if (detail.ProceduralInletCount > 0)
        {
            CarveCoastalInlets(plan, landThreshold, detail, stageSeed + 50);
        }

        PerturbMapEdgeCoastline(plan, landThreshold, config.MinOceanBorderCells, stageSeed + 180);
        CoastlineCleanupStage.Execute(plan, config, recomputeCoastDistance: false);
        CoastDistanceStage.Execute(plan, config);
    }

    /// <summary>
    /// Carves tapered coastal inlets at river mouths after river paths are traced.
    /// Recomputes coast distance so downstream land/coast classification stays consistent.
    /// </summary>
    public static void CarveRiverMouthInlets(
        IslandPlan plan,
        IslandDefinition config,
        ulong stageSeed)
    {
        if (config.UseLegacyIslandMask || !config.IslandShape.CoastlineDetail.PreferRiverMouthInlets)
        {
            return;
        }

        if (plan.IsRiverCell.Length != plan.Width * plan.Height)
        {
            return;
        }

        float landThreshold = config.IslandShape.LandThreshold;
        var random = new DeterministicRandom(stageSeed);
        var mouths = new List<(int X, int Y, float NormalX, float NormalY)>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (!plan.IsRiverCell[index] || plan.IslandMask[index] <= landThreshold)
                {
                    continue;
                }

                if (!IsCoastLandCell(plan, x, y, landThreshold))
                {
                    continue;
                }

                if (!TryComputeInwardNormal(plan, x, y, landThreshold, out float normalX, out float normalY))
                {
                    continue;
                }

                mouths.Add((x, y, normalX, normalY));
            }
        }

        if (mouths.Count == 0)
        {
            return;
        }

        int minSpacing = Math.Max(10, Math.Min(plan.Width, plan.Height) / 20);
        var placed = new List<(int X, int Y)>();

        foreach ((int x, int y, float normalX, float normalY) in mouths)
        {
            bool tooClose = placed.Any(site =>
                Math.Abs(site.X - x) + Math.Abs(site.Y - y) < minSpacing);
            if (tooClose)
            {
                continue;
            }

            placed.Add((x, y));
            float inletDepth = 5f + random.NextFloat() * 8f;
            float inletWidth = 3f + random.NextFloat() * 5f;
            float centerX = x + normalX * inletDepth * 0.55f;
            float centerY = y + normalY * inletDepth * 0.55f;
            float tangentX = -normalY;
            float tangentY = normalX;
            float rotation = MathF.Atan2(tangentY, tangentX);

            CarveTaperedInlet(
                plan,
                landThreshold,
                centerX,
                centerY,
                radiusAlongShore: inletWidth,
                radiusIntoLand: inletDepth,
                rotationRadians: rotation);
        }

        CoastDistanceStage.Execute(plan, config);
    }

    private static void PerturbMapEdgeCoastline(
        IslandPlan plan,
        float landThreshold,
        int border,
        ulong stageSeed)
    {
        int edgeBand = Math.Max(20, border + 12);
        var scratch = new float[plan.IslandMask.Length];
        Array.Copy(plan.IslandMask, scratch, plan.IslandMask.Length);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int edgeDist = Math.Min(
                    Math.Min(x, y),
                    Math.Min(plan.Width - 1 - x, plan.Height - 1 - y));
                if (edgeDist > edgeBand)
                {
                    continue;
                }

                int index = y * plan.Width + x;
                if (!IsNearCoast(plan, x, y, landThreshold, maxCells: 4) && edgeDist > border)
                {
                    continue;
                }

                float noise = NoiseUtility.Fbm(stageSeed, x * 0.13f, y * 0.13f, octaves: 3);
                float strength = (1f - edgeDist / (float)edgeBand) * 0.14f;
                float delta = (noise - 0.5f) * strength;
                scratch[index] = Math.Clamp(plan.IslandMask[index] + delta, 0f, 1.25f);
            }
        }

        plan.IslandMask = scratch;
    }

    private static void ApplyCoastalCellularAutomata(
        IslandPlan plan,
        float landThreshold,
        IslandCoastlineDetailDefinition detail,
        ulong stageSeed)
    {
        int iterations = Math.Clamp(detail.CellularAutomataIterations, 0, 6);
        if (iterations == 0)
        {
            return;
        }

        for (int pass = 0; pass < iterations; pass++)
        {
            var next = new float[plan.IslandMask.Length];
            Array.Copy(plan.IslandMask, next, plan.IslandMask.Length);

            for (int y = 1; y < plan.Height - 1; y++)
            {
                for (int x = 1; x < plan.Width - 1; x++)
                {
                    if (!IsNearCoast(plan, x, y, landThreshold, maxCells: 5))
                    {
                        continue;
                    }

                    int index = y * plan.Width + x;
                    bool isLand = plan.IslandMask[index] > landThreshold;
                    int landNeighbors = CountLandNeighbors(plan, x, y, landThreshold);
                    float noise = NoiseUtility.Fbm(stageSeed + (uint)pass, x * 0.11f, y * 0.11f, octaves: 2);

                    if (isLand && landNeighbors <= 3 && noise > 0.38f)
                    {
                        next[index] = landThreshold * 0.15f;
                    }
                    else if (!isLand && landNeighbors >= 5 && noise < 0.28f)
                    {
                        next[index] = landThreshold * 1.7f;
                    }
                    else if (isLand && landNeighbors == 4)
                    {
                        if (noise > 0.68f)
                        {
                            next[index] = landThreshold * 0.35f;
                        }
                        else if (noise < 0.14f)
                        {
                            next[index] = landThreshold * 1.45f;
                        }
                    }
                }
            }

            plan.IslandMask = next;
        }
    }

    private static void CarveCoastalInlets(
        IslandPlan plan,
        float landThreshold,
        IslandCoastlineDetailDefinition detail,
        ulong stageSeed)
    {
        int targetInlets = Math.Clamp(detail.ProceduralInletCount, 0, 24);
        if (targetInlets == 0)
        {
            return;
        }

        var random = new DeterministicRandom(stageSeed);
        var candidates = new List<(int X, int Y, float NormalX, float NormalY, float Score)>();

        for (int y = 1; y < plan.Height - 1; y++)
        {
            for (int x = 1; x < plan.Width - 1; x++)
            {
                int index = y * plan.Width + x;
                if (plan.IslandMask[index] <= landThreshold)
                {
                    continue;
                }

                if (!IsCoastLandCell(plan, x, y, landThreshold))
                {
                    continue;
                }

                if (!TryComputeInwardNormal(plan, x, y, landThreshold, out float normalX, out float normalY))
                {
                    continue;
                }

                float score = NoiseUtility.Fbm(stageSeed + 90, x * 0.09f, y * 0.09f, octaves: 2);
                score += NoiseUtility.Fbm(stageSeed + 120, normalX * 3f, normalY * 3f, octaves: 1) * 0.25f;
                candidates.Add((x, y, normalX, normalY, score));
            }
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        int minSpacing = Math.Max(8, Math.Min(plan.Width, plan.Height) / 24);
        var placed = new List<(int X, int Y)>();

        foreach ((int x, int y, float normalX, float normalY, _) in candidates)
        {
            if (placed.Count >= targetInlets)
            {
                break;
            }

            bool tooClose = placed.Any(site =>
                Math.Abs(site.X - x) + Math.Abs(site.Y - y) < minSpacing);
            if (tooClose)
            {
                continue;
            }

            placed.Add((x, y));
            float inletDepth = 4f + random.NextFloat() * 7f;
            float inletWidth = 2.5f + random.NextFloat() * 4f;
            float centerX = x + normalX * inletDepth * 0.55f;
            float centerY = y + normalY * inletDepth * 0.55f;
            float tangentX = -normalY;
            float tangentY = normalX;
            float rotation = MathF.Atan2(tangentY, tangentX);

            CarveTaperedInlet(
                plan,
                landThreshold,
                centerX,
                centerY,
                radiusAlongShore: inletWidth,
                radiusIntoLand: inletDepth,
                rotationRadians: rotation);
        }
    }

    private static void CarveTaperedInlet(
        IslandPlan plan,
        float landThreshold,
        float centerX,
        float centerY,
        float radiusAlongShore,
        float radiusIntoLand,
        float rotationRadians)
    {
        int boundX = (int)MathF.Ceiling(MathF.Max(radiusAlongShore, radiusIntoLand)) + 2;
        int boundY = boundX;
        float cos = MathF.Cos(rotationRadians);
        float sin = MathF.Sin(rotationRadians);

        int minX = Math.Max(0, (int)MathF.Floor(centerX - boundX));
        int maxX = Math.Min(plan.Width - 1, (int)MathF.Ceiling(centerX + boundX));
        int minY = Math.Max(0, (int)MathF.Floor(centerY - boundY));
        int maxY = Math.Min(plan.Height - 1, (int)MathF.Ceiling(centerY + boundY));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                float localX = dx * cos + dy * sin;
                float localY = -dx * sin + dy * cos;

                // Wide at the mouth (ocean side), narrowing inland.
                float inwardFraction = Math.Clamp((localY + radiusIntoLand) / (2f * radiusIntoLand), 0f, 1f);
                float taperedRadiusAlongShore = radiusAlongShore * (1f - inwardFraction * 0.75f);

                float norm = (localX * localX) / (taperedRadiusAlongShore * taperedRadiusAlongShore)
                    + (localY * localY) / (radiusIntoLand * radiusIntoLand);
                if (norm > 1f)
                {
                    continue;
                }

                int index = y * plan.Width + x;
                float carve = 1f - norm;
                carve *= carve;
                plan.IslandMask[index] = MathF.Min(
                    plan.IslandMask[index],
                    landThreshold * (0.15f - carve * 0.2f));
            }
        }
    }

    private static bool IsNearCoast(IslandPlan plan, int x, int y, float landThreshold, int maxCells)
    {
        var queue = new Queue<(int X, int Y, int Depth)>();
        var visited = new bool[plan.Width * plan.Height];
        int start = y * plan.Width + x;
        queue.Enqueue((x, y, 0));
        visited[start] = true;

        while (queue.Count > 0)
        {
            (int cx, int cy, int depth) = queue.Dequeue();
            int index = cy * plan.Width + cx;
            bool isLand = plan.IslandMask[index] > landThreshold;

            foreach ((int dx, int dy) in CardinalNeighbors)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
                {
                    return true;
                }

                int neighbor = ny * plan.Width + nx;
                bool neighborLand = plan.IslandMask[neighbor] > landThreshold;
                if (isLand != neighborLand)
                {
                    return true;
                }

                if (depth < maxCells && !visited[neighbor])
                {
                    visited[neighbor] = true;
                    queue.Enqueue((nx, ny, depth + 1));
                }
            }
        }

        return false;
    }

    private static bool IsCoastLandCell(IslandPlan plan, int x, int y, float landThreshold)
    {
        int index = y * plan.Width + x;
        if (plan.IslandMask[index] <= landThreshold)
        {
            return false;
        }

        foreach ((int dx, int dy) in CardinalNeighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
            {
                return true;
            }

            if (plan.IslandMask[ny * plan.Width + nx] <= landThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryComputeInwardNormal(
        IslandPlan plan,
        int x,
        int y,
        float landThreshold,
        out float normalX,
        out float normalY)
    {
        float sumX = 0f;
        float sumY = 0f;
        int count = 0;

        foreach ((int dx, int dy) in CardinalNeighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
            {
                continue;
            }

            if (plan.IslandMask[ny * plan.Width + nx] <= landThreshold)
            {
                sumX += dx;
                sumY += dy;
                count++;
            }
        }

        if (count == 0)
        {
            normalX = 0f;
            normalY = 0f;
            return false;
        }

        normalX = sumX / count;
        normalY = sumY / count;
        float length = MathF.Sqrt(normalX * normalX + normalY * normalY);
        if (length < 0.001f)
        {
            return false;
        }

        normalX /= length;
        normalY /= length;
        return true;
    }

    private static int CountLandNeighbors(IslandPlan plan, int x, int y, float landThreshold)
    {
        int count = 0;
        foreach ((int dx, int dy) in MooreNeighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
            {
                continue;
            }

            if (plan.IslandMask[ny * plan.Width + nx] > landThreshold)
            {
                count++;
            }
        }

        return count;
    }
}
