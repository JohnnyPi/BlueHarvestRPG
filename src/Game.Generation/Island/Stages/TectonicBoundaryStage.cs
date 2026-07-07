using Game.Content.Definitions;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Resolves plate boundaries and applies elevation changes from subduction, collision, and rifting.
/// </summary>
public static class TectonicBoundaryStage
{
    private const uint StageSalt = 11;

    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.PlateBoundaries.Clear();
        _ = SeedUtility.DeriveStage(seed, StageSalt);

        var regionById = plan.Regions.ToDictionary(region => region.Id);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int regionId = plan.GetRegionId(x, y);
                if (!regionById.TryGetValue(regionId, out IslandRegion? plateA))
                {
                    continue;
                }

                foreach ((int dx, int dy) in Neighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!plan.Contains(nx, ny))
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

                    plan.PlateBoundaries.Add(new PlateBoundarySegment
                    {
                        PlateAId = plateA.Id,
                        PlateBId = plateB.Id,
                        Type = boundaryType,
                        CellX = x,
                        CellY = y
                    });

                    ApplyBoundaryForces(plan, x, y, nx, ny, plateA, plateB, boundaryType, config);
                }
            }
        }
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
        int nx,
        int ny,
        IslandRegion plateA,
        IslandRegion plateB,
        PlateBoundaryType boundaryType,
        IslandDefinition config)
    {
        ref IslandCellData cellA = ref plan.GetCell(x, y);
        ref IslandCellData cellB = ref plan.GetCell(nx, ny);

        switch (boundaryType)
        {
            case PlateBoundaryType.ConvergentCollision:
                cellA.TectonicUplift += config.CollisionUplift;
                cellB.TectonicUplift += config.CollisionUplift;
                cellA.BoundaryType = PlateBoundaryType.ConvergentCollision;
                cellB.BoundaryType = PlateBoundaryType.ConvergentCollision;
                break;

            case PlateBoundaryType.ConvergentSubduction:
                bool aSubducts = !plateA.IsContinental && plateB.IsContinental;
                bool bSubducts = !plateB.IsContinental && plateA.IsContinental;
                if (aSubducts)
                {
                    cellA.TectonicUplift -= config.SubductionUplift * 0.45f;
                    cellB.TectonicUplift += config.SubductionUplift;
                    cellB.VolcanicActivity += config.SubductionUplift;
                }
                else if (bSubducts)
                {
                    cellB.TectonicUplift -= config.SubductionUplift * 0.45f;
                    cellA.TectonicUplift += config.SubductionUplift;
                    cellA.VolcanicActivity += config.SubductionUplift;
                }
                else
                {
                    cellA.TectonicUplift += config.SubductionUplift * 0.5f;
                    cellB.TectonicUplift += config.SubductionUplift * 0.5f;
                }

                cellA.BoundaryType = PlateBoundaryType.ConvergentSubduction;
                cellB.BoundaryType = PlateBoundaryType.ConvergentSubduction;
                break;

            case PlateBoundaryType.Divergent:
                float ridge = config.DivergentRidgeBoost;
                if (!cellA.IsLand)
                {
                    cellA.TectonicUplift += ridge;
                }
                else
                {
                    cellA.TectonicUplift -= ridge * 0.5f;
                    cellA.VolcanicActivity += ridge;
                }

                if (!cellB.IsLand)
                {
                    cellB.TectonicUplift += ridge;
                }
                else
                {
                    cellB.TectonicUplift -= ridge * 0.5f;
                    cellB.VolcanicActivity += ridge;
                }

                cellA.BoundaryType = PlateBoundaryType.Divergent;
                cellB.BoundaryType = PlateBoundaryType.Divergent;
                break;

            case PlateBoundaryType.Transform:
                cellA.BoundaryType = PlateBoundaryType.Transform;
                cellB.BoundaryType = PlateBoundaryType.Transform;
                break;
        }
    }
}
