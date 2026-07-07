using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class RegionBiomeStage
{
    private const uint StageSalt = 3;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        _ = config;
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        foreach (IslandRegion region in plan.Regions)
        {
            ref IslandCellData siteCell = ref plan.GetCell(region.SiteX, region.SiteY);
            if (!siteCell.IsLand)
            {
                region.Theme = BiomeId.Ocean;
                continue;
            }

            region.Theme = ClassifyRegionTheme(siteCell, region, random);
        }

        BalanceRegionThemes(plan, random);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);

                if (!cell.IsLand)
                {
                    cell.Biome = BiomeId.Ocean;
                    continue;
                }

                if (cell.IsCoast)
                {
                    cell.Biome = BiomeId.Beach;
                    continue;
                }

                if (cell.VolcanicActivity > 0.18f || cell.Elevation > 0.9f)
                {
                    cell.Biome = BiomeId.Volcanic;
                    continue;
                }

                if (cell.Elevation > 0.78f)
                {
                    cell.Biome = cell.BoundaryType == PlateBoundaryType.ConvergentCollision
                        ? BiomeId.Mountains
                        : BiomeId.Hills;
                    continue;
                }

                int regionId = plan.GetRegionId(x, y);
                IslandRegion? region = plan.Regions.FirstOrDefault(r => r.Id == regionId);
                BiomeId theme = region?.Theme ?? BiomeId.Plains;
                cell.Biome = ApplyCellVariation(theme, cell, stageSeed, x, y);
            }
        }
    }

    private static void BalanceRegionThemes(IslandPlan plan, DeterministicRandom random)
    {
        var landRegions = plan.Regions.Where(r => r.Theme != BiomeId.Ocean).ToList();
        if (landRegions.Count == 0)
        {
            return;
        }

        var targets = new Dictionary<BiomeId, int>
        {
            [BiomeId.Plains] = Math.Max(1, landRegions.Count * 20 / 100),
            [BiomeId.Forest] = Math.Max(1, landRegions.Count * 18 / 100),
            [BiomeId.Jungle] = Math.Max(1, landRegions.Count * 15 / 100),
            [BiomeId.Hills] = Math.Max(1, landRegions.Count * 12 / 100),
            [BiomeId.Swamp] = Math.Max(1, landRegions.Count * 10 / 100),
            [BiomeId.Mountains] = Math.Max(1, landRegions.Count * 8 / 100),
            [BiomeId.Volcanic] = Math.Max(1, landRegions.Count * 5 / 100),
        };

        foreach ((BiomeId biome, int target) in targets)
        {
            int current = landRegions.Count(r => r.Theme == biome);
            while (current < target)
            {
                IslandRegion? donor = landRegions
                    .Where(r => r.Theme != biome && landRegions.Count(x => x.Theme == r.Theme) > 1)
                    .OrderBy(_ => random.NextFloat())
                    .FirstOrDefault();

                if (donor is null)
                {
                    break;
                }

                donor.Theme = biome;
                current++;
            }
        }
    }

    private static BiomeId ClassifyRegionTheme(IslandCellData siteCell, IslandRegion region, DeterministicRandom random)
    {
        float elevation = siteCell.Elevation;
        float moisture = siteCell.Moisture;
        float temperature = siteCell.Temperature;
        float regionRoll = random.NextFloat();

        if (region.IsSatelliteIsland)
        {
            return regionRoll < 0.45f ? BiomeId.Jungle : BiomeId.Hills;
        }

        if (siteCell.VolcanicActivity > 0.15f)
        {
            return BiomeId.Volcanic;
        }

        if (elevation > 0.82f)
        {
            return regionRoll < 0.35f ? BiomeId.Volcanic : BiomeId.Mountains;
        }

        if (elevation > 0.68f)
        {
            return BiomeId.Hills;
        }

        if (moisture > 0.75f)
        {
            return BiomeId.Swamp;
        }

        if (temperature > 0.62f && moisture > 0.58f)
        {
            return regionRoll < 0.55f ? BiomeId.Jungle : BiomeId.Forest;
        }

        if (temperature < 0.35f && moisture > 0.55f)
        {
            return BiomeId.Swamp;
        }

        if (moisture > 0.62f)
        {
            return regionRoll < 0.5f ? BiomeId.Forest : BiomeId.Jungle;
        }

        if (temperature > 0.55f && moisture < 0.42f)
        {
            return BiomeId.Plains;
        }

        if (moisture > 0.48f)
        {
            return BiomeId.Forest;
        }

        return BiomeId.Plains;
    }

    private static BiomeId ApplyCellVariation(BiomeId theme, IslandCellData cell, ulong stageSeed, int x, int y)
    {
        float jitter = ValueNoise.Sample(stageSeed + 17, x * 0.11f, y * 0.11f, octaves: 2);
        if (jitter > 0.78f)
        {
            return theme switch
            {
                BiomeId.Forest => BiomeId.Jungle,
                BiomeId.Jungle => BiomeId.Forest,
                BiomeId.Plains => BiomeId.Hills,
                BiomeId.Hills => BiomeId.Plains,
                BiomeId.Swamp => BiomeId.Forest,
                _ => theme
            };
        }

        if (jitter < 0.18f && cell.Moisture < 0.42f && cell.Elevation < 0.62f)
        {
            return theme is BiomeId.Forest or BiomeId.Jungle ? BiomeId.Plains : theme;
        }

        return theme;
    }
}
