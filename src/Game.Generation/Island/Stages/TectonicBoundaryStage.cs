using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Resolves plate boundaries and applies subtle elevation changes from plate interactions.
/// Boundary overlays are sparse and land-only so Voronoi edges do not paint straight lines.
/// </summary>
public static class TectonicBoundaryStage
{
    private const uint StageSalt = 11;
    private const int UpliftFalloffRadius = 2;

    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.PlateBoundaries.Clear();
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        var regionById = plan.Regions.ToDictionary(region => region.Id);
        var boundaryUplift = new float[plan.Width * plan.Height];
        var boundaryTypes = new PlateBoundaryType[plan.Width * plan.Height];

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!IsInteriorCell(plan, x, y, config))
                {
                    continue;
                }

                int regionId = plan.GetRegionId(x, y);
                if (!regionById.TryGetValue(regionId, out IslandRegion? plateA))
                {
                    continue;
                }

                foreach ((int dx, int dy) in Neighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!plan.Contains(nx, ny) || !IsInteriorCell(plan, nx, ny, config))
                    {
                        continue;
                    }

                    int neighborRegionId = plan.GetRegionId(nx, ny);
                    if (neighborRegionId == regionId || !regionById.TryGetValue(neighborRegionId, out IslandRegion? plateB))
                    {
                        continue;
                    }

                    if (plateA.Id > plateB.Id)
                    {
                        continue;
                    }

                    PlateBoundaryType boundaryType = ClassifyBoundary(plateA, plateB, dx, dy, config.ConvergenceThreshold);
                    if (boundaryType == PlateBoundaryType.None)
                    {
                        continue;
                    }

                    ref IslandCellData cellA = ref plan.GetCell(x, y);
                    ref IslandCellData cellB = ref plan.GetCell(nx, ny);
                    if (!cellA.IsLand && !cellB.IsLand)
                    {
                        continue;
                    }

                    plan.PlateBoundaries.Add(new PlateBoundarySegment
                    {
                        PlateAId = plateA.Id,
                        PlateBId = plateB.Id,
                        Type = boundaryType,
                        CellX = x,
                        CellY = y
                    });

                    ApplyBoundaryForces(
                        plan,
                        x,
                        y,
                        boundaryType,
                        config,
                        boundaryUplift,
                        boundaryTypes,
                        stageSeed);
                }
            }
        }

        PropagateSoftUplift(plan, boundaryUplift, boundaryTypes, stageSeed, config);
    }

    private static bool IsInteriorCell(IslandPlan plan, int x, int y, IslandDefinition config)
    {
        int border = Math.Max(0, config.MinOceanBorderCells);
        return x >= border && y >= border && x < plan.Width - border && y < plan.Height - border;
    }

    private static PlateBoundaryType ClassifyBoundary(
        IslandRegion plateA,
        IslandRegion plateB,
        int dx,
        int dy,
        float convergenceThreshold)
    {
        float normalX = dx;
        float normalY = dy;
        float length = MathF.Sqrt(normalX * normalX + normalY * normalY);
        if (length <= 0f)
        {
            return PlateBoundaryType.None;
        }

        normalX /= length;
        normalY /= length;

        (float ax, float ay) = plateA.MotionVector;
        (float bx, float by) = plateB.MotionVector;
        float relativeX = bx - ax;
        float relativeY = by - ay;
        float convergence = relativeX * normalX + relativeY * normalY;

        if (convergence > convergenceThreshold)
        {
            if (plateA.IsContinental && plateB.IsContinental)
            {
                return PlateBoundaryType.ConvergentCollision;
            }

            return PlateBoundaryType.ConvergentSubduction;
        }

        if (convergence < -convergenceThreshold)
        {
            return PlateBoundaryType.Divergent;
        }

        if (MathF.Abs(convergence) <= convergenceThreshold * 0.35f)
        {
            return PlateBoundaryType.Transform;
        }

        return PlateBoundaryType.None;
    }

    private static void ApplyBoundaryForces(
        IslandPlan plan,
        int x,
        int y,
        PlateBoundaryType boundaryType,
        IslandDefinition config,
        float[] boundaryUplift,
        PlateBoundaryType[] boundaryTypes,
        ulong stageSeed)
    {
        int index = y * plan.Width + x;
        float edgeWeight = plan.VoronoiEdge.Length > index
            ? Math.Clamp(plan.VoronoiEdge[index] * 6f, 0.1f, 1f)
            : 0.5f;

        float sparse = NoiseUtility.Fbm(stageSeed + 31, x * 0.13f, y * 0.13f, octaves: 2);
        if (sparse < 0.42f && boundaryType != PlateBoundaryType.ConvergentCollision)
        {
            return;
        }

        switch (boundaryType)
        {
            case PlateBoundaryType.ConvergentCollision:
                Accumulate(boundaryUplift, index, config.CollisionUplift * edgeWeight * 0.65f);
                boundaryTypes[index] = PlateBoundaryType.ConvergentCollision;
                break;

            case PlateBoundaryType.ConvergentSubduction:
                Accumulate(boundaryUplift, index, config.SubductionUplift * edgeWeight * 0.35f);
                break;

            case PlateBoundaryType.Divergent:
                Accumulate(boundaryUplift, index, config.DivergentRidgeBoost * edgeWeight * 0.25f);
                break;

            case PlateBoundaryType.Transform:
                break;
        }
    }

    private static void PropagateSoftUplift(
        IslandPlan plan,
        float[] boundaryUplift,
        PlateBoundaryType[] boundaryTypes,
        ulong stageSeed,
        IslandDefinition config)
    {
        var propagatedUplift = new float[boundaryUplift.Length];

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (MathF.Abs(boundaryUplift[index]) <= 0f)
                {
                    continue;
                }

                float jitter = NoiseUtility.Fbm(stageSeed + 30, x * 0.09f, y * 0.09f, octaves: 2) * 0.04f;

                for (int dy = -UpliftFalloffRadius; dy <= UpliftFalloffRadius; dy++)
                {
                    for (int dx = -UpliftFalloffRadius; dx <= UpliftFalloffRadius; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (!plan.Contains(nx, ny) || !IsInteriorCell(plan, nx, ny, config))
                        {
                            continue;
                        }

                        if (!plan.IsLand(nx, ny))
                        {
                            continue;
                        }

                        float dist = MathF.Sqrt(dx * dx + dy * dy);
                        if (dist > UpliftFalloffRadius)
                        {
                            continue;
                        }

                        float falloff = 1f - dist / (UpliftFalloffRadius + 0.5f);
                        int neighborIndex = ny * plan.Width + nx;
                        propagatedUplift[neighborIndex] += (boundaryUplift[index] + jitter) * falloff;
                    }
                }
            }
        }

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.TectonicUplift += propagatedUplift[index];

                if (boundaryTypes[index] == PlateBoundaryType.ConvergentCollision &&
                    cell.IsLand &&
                    IsInteriorCell(plan, x, y, config))
                {
                    cell.BoundaryType = PlateBoundaryType.ConvergentCollision;
                }
            }
        }
    }

    private static void Accumulate(float[] values, int index, float amount)
    {
        values[index] += amount;
    }
}
