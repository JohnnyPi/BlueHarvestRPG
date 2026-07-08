using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class RegionBiomeStage
{
    private const uint StageSalt = 3;

    public static void Execute(IslandPlan plan, IslandDefinition config, BiomeRulesDefinition biomeRules, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);
        int blendCount = Math.Clamp(config.BiomeBlendNeighborCount, 1, 4);
        var regionById = plan.Regions.ToDictionary(region => region.Id);
        bool useCoastDistance = !config.UseLegacyIslandMask && plan.CoastDistance.Length == plan.Width * plan.Height;

        foreach (IslandRegion region in plan.Regions)
        {
            ref IslandCellData siteCell = ref plan.GetCell(region.SiteX, region.SiteY);
            if (!siteCell.IsLand)
            {
                region.Theme = BiomeId.Ocean;
                continue;
            }

            region.Theme = ClassifyRegionTheme(siteCell, region, biomeRules, random);
        }

        BalanceRegionThemes(plan, random);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                int index = y * plan.Width + x;

                if (!cell.IsLand)
                {
                    if (cell.Biome is not (BiomeId.ShallowWater or BiomeId.Reef))
                    {
                        cell.Biome = BiomeId.Ocean;
                    }

                    continue;
                }

                if (useCoastDistance)
                {
                    float coastDistance = plan.CoastDistance[index];
                    float concavity = plan.Concavity.Length > index ? plan.Concavity[index] : 0f;
                    float beachWidth = config.BeachCoastDistance + concavity * 0.03f;
                    float riverInfluence = plan.RiverInfluence.Length > index ? plan.RiverInfluence[index] : 0f;

                    if (coastDistance <= beachWidth)
                    {
                        cell.Biome = ResolveCoastalBiome(plan, index, cell);
                        continue;
                    }

                    if (riverInfluence > 0.45f && cell.Elevation < biomeRules.HillsMinElevation && concavity > 0.05f)
                    {
                        cell.Biome = BiomeId.Swamp;
                        continue;
                    }

                    if (riverInfluence > 0.35f && cell.Temperature > 0.55f && cell.Moisture > 0.5f)
                    {
                        cell.Biome = BiomeId.Jungle;
                        continue;
                    }

                    if (coastDistance <= config.InlandCoastDistance + 0.03f
                        && cell.Moisture > biomeRules.SwampMinMoisture - 0.05f
                        && concavity > 0.1f)
                    {
                        cell.Biome = BiomeId.Swamp;
                        continue;
                    }
                }
                else if (cell.IsCoast)
                {
                    cell.Biome = BiomeId.Beach;
                    continue;
                }

                if (VolcanicConeUtility.TryGetNearestConeDistance(plan, x, y, out float coneDistance))
                {
                    if (coneDistance <= VolcanicConeUtility.LavaCoreRadiusFraction)
                    {
                        cell.Biome = BiomeId.Volcanic;
                        continue;
                    }

                    if (coneDistance <= VolcanicConeUtility.MountainRingRadiusFraction)
                    {
                        cell.Biome = BiomeId.Mountains;
                        continue;
                    }

                    if (coneDistance <= VolcanicConeUtility.HillRingRadiusFraction)
                    {
                        cell.Biome = BiomeId.Hills;
                        continue;
                    }
                }

                if (cell.VolcanicActivity > 0.2f && IsInVolcanicLavaCore(plan, x, y))
                {
                    cell.Biome = BiomeId.Volcanic;
                    continue;
                }

                if (cell.Elevation > biomeRules.MountainsMinElevation + 0.06f)
                {
                    cell.Biome = cell.BoundaryType == PlateBoundaryType.ConvergentCollision
                        ? BiomeId.Mountains
                        : BiomeId.Hills;
                    continue;
                }

                if (cell.Elevation > biomeRules.HillsMinElevation + 0.10f)
                {
                    cell.Biome = cell.BoundaryType == PlateBoundaryType.ConvergentCollision
                        ? BiomeId.Mountains
                        : BiomeId.Hills;
                    continue;
                }

                int blendBase = index * blendCount;
                BiomeId blendedTheme = BlendRegionThemes(plan, regionById, x, y, blendBase, blendCount, cell);
                cell.Biome = ApplyCellVariation(blendedTheme, cell, stageSeed, x, y, config, plan, index);
            }
        }
    }

    private static bool IsInVolcanicLavaCore(IslandPlan plan, int x, int y)
    {
        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            if (VolcanicConeUtility.IsInsideLavaCore(site, x, y))
            {
                return true;
            }
        }

        return false;
    }

    private static BiomeId BlendRegionThemes(
        IslandPlan plan,
        Dictionary<int, IslandRegion> regionById,
        int x,
        int y,
        int blendBase,
        int blendCount,
        IslandCellData cell)
    {
        var scores = new Dictionary<BiomeId, float>();

        for (int i = 0; i < blendCount; i++)
        {
            int regionId = plan.VoronoiBlendRegionIds.Length > blendBase + i
                ? plan.VoronoiBlendRegionIds[blendBase + i]
                : plan.GetRegionId(x, y);
            float weight = plan.VoronoiBlendWeights.Length > blendBase + i
                ? plan.VoronoiBlendWeights[blendBase + i]
                : 1f;

            if (!regionById.TryGetValue(regionId, out IslandRegion? region))
            {
                continue;
            }

            BiomeId theme = region.Theme;
            scores.TryGetValue(theme, out float current);
            scores[theme] = current + weight;
        }

        if (scores.Count == 0)
        {
            return BiomeId.Plains;
        }

        BiomeId best = BiomeId.Plains;
        float bestScore = float.MinValue;
        foreach ((BiomeId biome, float score) in scores)
        {
            float adjusted = score + BiomeSuitabilityHelper.GetClimateAffinity(biome, cell) * 0.15f;
            if (adjusted > bestScore)
            {
                bestScore = adjusted;
                best = biome;
            }
        }

        return best;
    }

    private static float BiomeClimateAffinity(BiomeId biome, IslandCellData cell)
        => BiomeSuitabilityHelper.GetClimateAffinity(biome, cell);

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
            [BiomeId.Hills] = Math.Max(1, landRegions.Count * 14 / 100),
            [BiomeId.Swamp] = Math.Max(1, landRegions.Count * 10 / 100),
            [BiomeId.Mountains] = Math.Max(1, landRegions.Count * 10 / 100),
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

    private static BiomeId ClassifyRegionTheme(
        IslandCellData siteCell,
        IslandRegion region,
        BiomeRulesDefinition biomeRules,
        DeterministicRandom random)
    {
        float elevation = siteCell.Elevation;
        float moisture = siteCell.Moisture;
        float temperature = siteCell.Temperature;
        float regionRoll = random.NextFloat();

        if (region.IsSatelliteIsland)
        {
            return regionRoll < 0.45f ? BiomeId.Jungle : BiomeId.Hills;
        }

        if (siteCell.VolcanicActivity > 0.25f)
        {
            return BiomeId.Hills;
        }

        if (elevation > biomeRules.MountainsMinElevation)
        {
            return BiomeId.Mountains;
        }

        if (elevation > biomeRules.HillsMinElevation)
        {
            return BiomeId.Hills;
        }

        if (moisture > biomeRules.SwampMinMoisture)
        {
            return BiomeId.Swamp;
        }

        if (temperature > 0.62f && moisture > 0.58f)
        {
            return regionRoll < 0.70f ? BiomeId.Jungle : BiomeId.Forest;
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

        if (moisture > biomeRules.ForestMinMoisture)
        {
            return BiomeId.Forest;
        }

        return BiomeId.Plains;
    }

    private static BiomeId ResolveCoastalBiome(IslandPlan plan, int index, IslandCellData cell)
    {
        if (plan.CoastalLandforms.Length > index)
        {
            return plan.CoastalLandforms[index] switch
            {
                CoastalLandform.Cliff or CoastalLandform.RockyCoast or CoastalLandform.Headland => BiomeId.Hills,
                CoastalLandform.Mangrove or CoastalLandform.Estuary or CoastalLandform.RiverMouth => BiomeId.Swamp,
                CoastalLandform.LagoonEdge or CoastalLandform.Bay => BiomeId.Beach,
                _ => BiomeId.Beach,
            };
        }

        return BiomeId.Beach;
    }

    private static BiomeId ApplyCellVariation(
        BiomeId theme,
        IslandCellData cell,
        ulong stageSeed,
        int x,
        int y,
        IslandDefinition config,
        IslandPlan plan,
        int index)
    {
        float riverInfluence = plan.RiverInfluence.Length > index ? plan.RiverInfluence[index] : 0f;
        if (riverInfluence > 0.4f)
        {
            return theme;
        }

        float jitter = ValueNoise.Sample(stageSeed + 17, x * 0.11f, y * 0.11f, octaves: 2);
        if (jitter > config.BiomeNoise.CellFlipStrength)
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

        if (jitter < config.BiomeNoise.CellDryFlipStrength && cell.Moisture < 0.42f && cell.Elevation < 0.62f)
        {
            return theme is BiomeId.Forest or BiomeId.Jungle ? BiomeId.Plains : theme;
        }

        return theme;
    }
}
