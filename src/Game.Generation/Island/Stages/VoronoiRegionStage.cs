using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Generation.Voronoi;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class VoronoiRegionStage
{
    private const uint StageSalt = 1;
    private const int MaxSiteAttempts = 48;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        plan.Regions.Clear();
        int regionCount = Math.Max(8, config.RegionCount);
        float minInland = config.UseLegacyIslandMask ? 0f : config.InlandCoastDistance * 0.75f;

        for (int i = 0; i < regionCount; i++)
        {
            if (!TryPickLandSite(plan, config, random, minInland, out int siteX, out int siteY))
            {
                siteX = random.NextInt(plan.Width);
                siteY = random.NextInt(plan.Height);
            }

            plan.Regions.Add(new IslandRegion
            {
                Id = i,
                SiteX = siteX,
                SiteY = siteY
            });
        }

        VoronoiField.ComputeField(plan, config, stageSeed, config.BiomeBlendNeighborCount);
    }

    private static bool TryPickLandSite(
        IslandPlan plan,
        IslandDefinition config,
        DeterministicRandom random,
        float minInland,
        out int siteX,
        out int siteY)
    {
        siteX = 0;
        siteY = 0;
        bool useCoastDistance = !config.UseLegacyIslandMask && plan.CoastDistance.Length == plan.Width * plan.Height;
        float landThreshold = config.UseLegacyIslandMask
            ? config.LandElevationThreshold * 0.5f
            : config.IslandShape.LandThreshold;

        float bestWeight = float.MinValue;
        bool found = false;

        for (int attempt = 0; attempt < MaxSiteAttempts; attempt++)
        {
            int x = random.NextInt(plan.Width);
            int y = random.NextInt(plan.Height);
            int index = y * plan.Width + x;

            bool isLand = useCoastDistance
                ? plan.CoastDistance[index] > minInland
                : plan.IslandMask.Length > index && plan.IslandMask[index] > landThreshold;

            if (!isLand)
            {
                continue;
            }

            float weight = useCoastDistance
                ? plan.CoastDistance[index]
                : plan.IslandMask[index];

            weight += random.NextFloat() * 0.05f;
            if (weight > bestWeight)
            {
                bestWeight = weight;
                siteX = x;
                siteY = y;
                found = true;
            }
        }

        return found;
    }
}
