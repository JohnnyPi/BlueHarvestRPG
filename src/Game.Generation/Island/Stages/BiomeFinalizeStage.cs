using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class BiomeFinalizeStage
{
    private const uint StageSalt = 9;

    public static void Execute(IslandPlan plan, ulong seed)
    {
        BiomeBalanceHelper.StampFacilityBiomes(plan);

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);
        BiomeId[] alternatives =
        [
            BiomeId.Plains,
            BiomeId.Hills,
            BiomeId.Jungle,
            BiomeId.Swamp,
            BiomeId.Forest,
            BiomeId.Mountains
        ];

        BiomeBalanceHelper.EnsureBiomeFloor(plan, stageSeed);

        for (int pass = 0; pass < 6; pass++)
        {
            if (!BiomeBalanceHelper.ReduceDominantBiome(plan, random, alternatives, maxShare: 0.38f))
            {
                break;
            }
        }

        BiomeBalanceHelper.StampFacilityBiomes(plan);
    }

    public static void EnsureRequiredBiomes(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        BiomeCoherenceDefinition coherence = config.BiomeCoherence;
        var minimumCounts = new Dictionary<BiomeId, int>
        {
            [BiomeId.Plains] = coherence.MinPatchPlains,
            [BiomeId.Forest] = coherence.MinPatchForest,
            [BiomeId.Jungle] = coherence.MinPatchJungle,
            [BiomeId.Swamp] = coherence.MinPatchSwamp,
            [BiomeId.Hills] = coherence.MinPatchHills,
            [BiomeId.Mountains] = coherence.MinPatchMountains,
        };
        BiomeBalanceHelper.EnsureBiomeFloor(plan, stageSeed, minimumCounts: minimumCounts);
    }

    public static void StampVolcanicCenters(IslandPlan plan)
    {
        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            if (!TryFindStampCenter(plan, site, out int centerX, out int centerY))
            {
                continue;
            }

            // Stamp the center plus eligible orthogonal neighbors so the volcanic
            // biome forms a small patch instead of a singleton cell.
            StampVolcanicCell(plan, site, centerX, centerY);
            StampVolcanicCell(plan, site, centerX + 1, centerY);
            StampVolcanicCell(plan, site, centerX - 1, centerY);
            StampVolcanicCell(plan, site, centerX, centerY + 1);
            StampVolcanicCell(plan, site, centerX, centerY - 1);
        }
    }

    private static bool TryFindStampCenter(IslandPlan plan, VolcanicSite site, out int centerX, out int centerY)
    {
        // Facilities and roads can claim the exact site cell (both favor the island
        // center); search outward for the nearest unclaimed land cell.
        for (int radius = 0; radius <= 4; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    int x = site.X + dx;
                    int y = site.Y + dy;
                    if (CanStampVolcanic(plan, site, x, y))
                    {
                        centerX = x;
                        centerY = y;
                        return true;
                    }
                }
            }
        }

        centerX = 0;
        centerY = 0;
        return false;
    }

    private static void StampVolcanicCell(IslandPlan plan, VolcanicSite site, int x, int y)
    {
        if (CanStampVolcanic(plan, site, x, y))
        {
            plan.GetCell(x, y).Biome = BiomeId.Volcanic;
        }
    }

    private static bool CanStampVolcanic(IslandPlan plan, VolcanicSite site, int x, int y)
    {
        if (!plan.Contains(x, y))
        {
            return false;
        }

        ref IslandCellData cell = ref plan.GetCell(x, y);
        return cell.IsLand
            && !cell.IsCoast
            && !BiomeBalanceHelper.HasEnterableRole(cell.Role)
            && (cell.VolcanicActivity > 0f || VolcanicConeUtility.IsInsideLavaCore(site, x, y));
    }
}
