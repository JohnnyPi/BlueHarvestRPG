using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class BiomeCoherenceStage
{
    private static readonly (int Dx, int Dy)[] FourWay = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int Dx, int Dy)[] EightWay =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1),
    ];

    public static void Execute(IslandPlan plan, IslandDefinition config, bool finalPass = false)
    {
        if (!config.BiomeCoherence.Enabled)
        {
            return;
        }

        var minPatchSizes = BuildMinPatchSizes(config);
        var neighbors = config.BiomeCoherence.UseEightWayNeighbors ? EightWay : FourWay;
        var regionThemes = BuildRegionThemeLookup(plan);

        foreach (BiomeId biome in LandBiomes)
        {
            int minSize = minPatchSizes.GetValueOrDefault(biome, 12);
            foreach (List<int> component in IslandQualityMetrics.FindBiomeComponents(plan, biome))
            {
                if (component.Count >= minSize)
                {
                    continue;
                }

                if (component.Any(index => IsProtectedCell(plan, index)))
                {
                    continue;
                }

                BiomeId replacement = FindBestNeighborBiome(plan, component, neighbors, regionThemes);
                foreach (int index in component)
                {
                    plan.Cells[index].Biome = replacement;
                }
            }
        }

        if (finalPass)
        {
            BiomeBalanceHelper.StampFacilityBiomes(plan);
        }
    }

    private static Dictionary<BiomeId, int> BuildMinPatchSizes(IslandDefinition config)
    {
        BiomeCoherenceDefinition coherence = config.BiomeCoherence;
        return new Dictionary<BiomeId, int>
        {
            [BiomeId.Beach] = coherence.MinPatchBeach,
            [BiomeId.Plains] = coherence.MinPatchPlains,
            [BiomeId.Forest] = coherence.MinPatchForest,
            [BiomeId.Jungle] = coherence.MinPatchJungle,
            [BiomeId.Swamp] = coherence.MinPatchSwamp,
            [BiomeId.Hills] = coherence.MinPatchHills,
            [BiomeId.Mountains] = coherence.MinPatchMountains,
            [BiomeId.Volcanic] = coherence.MinPatchVolcanic,
        };
    }

    private static Dictionary<int, BiomeId> BuildRegionThemeLookup(IslandPlan plan)
    {
        var lookup = new Dictionary<int, BiomeId>();
        foreach (IslandRegion region in plan.Regions)
        {
            lookup[region.Id] = region.Theme;
        }

        return lookup;
    }

    private static bool IsProtectedCell(IslandPlan plan, int index)
    {
        ref IslandCellData cell = ref plan.Cells[index];
        if (BiomeBalanceHelper.HasEnterableRole(cell.Role))
        {
            return true;
        }

        if (cell.Biome is BiomeId.Beach && cell.IsCoast)
        {
            return true;
        }

        if (cell.Biome == BiomeId.Volcanic && cell.VolcanicActivity > 0.2f)
        {
            return true;
        }

        if (plan.IsRiverCell.Length > index && plan.IsRiverCell[index])
        {
            return true;
        }

        int x = index % plan.Width;
        int y = index / plan.Width;
        return BiomeBalanceHelper.IsProtectedLandmarkCell(plan, x, y);
    }

    private static BiomeId FindBestNeighborBiome(
        IslandPlan plan,
        List<int> component,
        (int Dx, int Dy)[] neighbors,
        Dictionary<int, BiomeId> regionThemes)
    {
        var borderScores = new Dictionary<BiomeId, float>();
        var componentSet = new HashSet<int>(component);

        foreach (int index in component)
        {
            int x = index % plan.Width;
            int y = index / plan.Width;
            ref IslandCellData cell = ref plan.Cells[index];

            foreach ((int dx, int dy) in neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.Contains(nx, ny))
                {
                    continue;
                }

                int neighborIndex = ny * plan.Width + nx;
                if (componentSet.Contains(neighborIndex))
                {
                    continue;
                }

                ref IslandCellData neighbor = ref plan.Cells[neighborIndex];
                if (!neighbor.IsLand)
                {
                    continue;
                }

                BiomeId neighborBiome = neighbor.Biome;
                float regionBonus = regionThemes.TryGetValue(neighbor.RegionId, out BiomeId theme) && theme == neighborBiome
                    ? 0.35f
                    : 0f;
                float riverBonus = BiomeSuitabilityHelper.GetRiverSuitabilityBonus(
                    neighborBiome,
                    plan.RiverInfluence.Length > index ? plan.RiverInfluence[index] : 0f);

                float score = 3f
                    + BiomeSuitabilityHelper.ScoreBiomeForCell(neighborBiome, cell, regionBonus, riverBonus) * 2f;

                borderScores.TryGetValue(neighborBiome, out float current);
                borderScores[neighborBiome] = current + score;
            }
        }

        if (borderScores.Count == 0)
        {
            return BiomeId.Plains;
        }

        return borderScores
            .OrderByDescending(pair => pair.Value)
            .First()
            .Key;
    }

    private static readonly BiomeId[] LandBiomes =
    [
        BiomeId.Plains,
        BiomeId.Forest,
        BiomeId.Jungle,
        BiomeId.Swamp,
        BiomeId.Hills,
        BiomeId.Mountains,
        BiomeId.Volcanic,
    ];
}
