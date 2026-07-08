using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Generation.Voronoi;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class VoronoiRegionStage
{
    private const uint StageSalt = 1;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        plan.Regions.Clear();
        int regionCount = Math.Max(8, config.RegionCount);

        for (int i = 0; i < regionCount; i++)
        {
            plan.Regions.Add(new IslandRegion
            {
                Id = i,
                SiteX = random.NextInt(plan.Width),
                SiteY = random.NextInt(plan.Height)
            });
        }

        VoronoiField.ComputeField(plan, config, stageSeed, config.BiomeBlendNeighborCount);
    }
}
