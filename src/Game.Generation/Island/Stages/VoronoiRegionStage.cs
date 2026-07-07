using Game.Content.Definitions;
using Game.Generation.Noise;
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

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int nearestId = 0;
                int nearestDistSq = int.MaxValue;

                foreach (IslandRegion region in plan.Regions)
                {
                    int dx = x - region.SiteX;
                    int dy = y - region.SiteY;
                    int distSq = dx * dx + dy * dy;

                    if (distSq < nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearestId = region.Id;
                    }
                }

                plan.RegionIds[y * plan.Width + x] = nearestId;
            }
        }
    }
}
