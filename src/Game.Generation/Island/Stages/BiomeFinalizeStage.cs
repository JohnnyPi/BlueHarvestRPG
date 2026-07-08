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
            BiomeId.Mountains,
            BiomeId.Volcanic
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
}
