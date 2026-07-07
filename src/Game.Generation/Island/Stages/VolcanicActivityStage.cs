using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Places volcanic sites from subduction arcs, collision zones, rifts, and mantle plumes.
/// </summary>
public static class VolcanicActivityStage
{
    private const uint StageSalt = 12;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.VolcanicSites.Clear();
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        StampSubductionAndRiftVolcanoes(plan);
        StampCollisionVolcanoes(plan);
        StampMantlePlumes(plan, config, random);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand)
                {
                    continue;
                }

                if (cell.VolcanicActivity > 0.12f || cell.Elevation + cell.TectonicUplift > 0.88f)
                {
                    cell.VolcanicActivity = MathF.Max(cell.VolcanicActivity, 0.15f);
                }
            }
        }
    }

    private static void StampSubductionAndRiftVolcanoes(IslandPlan plan)
    {
        foreach (PlateBoundarySegment boundary in plan.PlateBoundaries)
        {
            ref IslandCellData cell = ref plan.GetCell(boundary.CellX, boundary.CellY);
            if (!cell.IsLand || cell.VolcanicActivity < 0.08f)
            {
                continue;
            }

            VolcanicOrigin origin = boundary.Type == PlateBoundaryType.Divergent
                ? VolcanicOrigin.RiftVolcano
                : VolcanicOrigin.SubductionArc;

            plan.VolcanicSites.Add(new VolcanicSite
            {
                X = boundary.CellX,
                Y = boundary.CellY,
                Origin = origin,
                Intensity = Math.Clamp(cell.VolcanicActivity, 0.1f, 1f)
            });

            cell.Elevation = Math.Clamp(cell.Elevation + cell.VolcanicActivity * 0.25f, 0f, 1.2f);
        }
    }

    private static void StampCollisionVolcanoes(IslandPlan plan)
    {
        foreach (PlateBoundarySegment boundary in plan.PlateBoundaries)
        {
            if (boundary.Type != PlateBoundaryType.ConvergentCollision)
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(boundary.CellX, boundary.CellY);
            if (!cell.IsLand)
            {
                continue;
            }

            plan.VolcanicSites.Add(new VolcanicSite
            {
                X = boundary.CellX,
                Y = boundary.CellY,
                Origin = VolcanicOrigin.CollisionVolcanism,
                Intensity = 0.35f
            });

            cell.VolcanicActivity = MathF.Max(cell.VolcanicActivity, 0.2f);
            cell.Elevation = Math.Clamp(cell.Elevation + 0.08f, 0f, 1.2f);
        }
    }

    private static void StampMantlePlumes(IslandPlan plan, IslandDefinition config, DeterministicRandom random)
    {
        int plumeCount = Math.Clamp(config.MantlePlumeCount, 0, 12);
        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float plumeRadiusCells = maxRadius * config.MantlePlumeRadius;

        for (int i = 0; i < plumeCount; i++)
        {
            int px = random.NextInt(plan.Width);
            int py = random.NextInt(plan.Height);
            if (!plan.IsLand(px, py))
            {
                continue;
            }

            plan.VolcanicSites.Add(new VolcanicSite
            {
                X = px,
                Y = py,
                Origin = VolcanicOrigin.MantlePlume,
                Intensity = config.MantlePlumeIntensity
            });

            int radius = Math.Max(2, (int)plumeRadiusCells);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = px + dx;
                    int y = py + dy;
                    if (!plan.Contains(x, y))
                    {
                        continue;
                    }

                    float distSq = dx * dx + dy * dy;
                    if (distSq > radius * radius)
                    {
                        continue;
                    }

                    float falloff = 1f - distSq / (radius * radius);
                    ref IslandCellData cell = ref plan.GetCell(x, y);
                    cell.VolcanicActivity += config.MantlePlumeIntensity * falloff;
                    cell.TectonicUplift += config.MantlePlumeIntensity * falloff * 0.5f;
                    cell.Elevation = Math.Clamp(cell.Elevation + config.MantlePlumeIntensity * falloff * 0.2f, 0f, 1.2f);
                }
            }
        }
    }
}
