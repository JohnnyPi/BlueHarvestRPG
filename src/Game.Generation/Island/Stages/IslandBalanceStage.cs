using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class IslandBalanceStage
{
    private const uint StageSalt = 13;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        for (int pass = 0; pass < config.BalancePassMaxIterations; pass++)
        {
            float wetShare = BiomeBalanceHelper.MeasureWetBiomeShare(plan);
            if (wetShare <= config.MaxWetBiomeShare)
            {
                break;
            }

            BiomeBalanceHelper.CorrectExcessWetness(plan, config.MaxWetBiomeShare, random);
        }

        for (int pass = 0; pass < config.BalancePassMaxIterations; pass++)
        {
            if (BiomeBalanceHelper.MeasureLandElevationStdDev(plan) >= config.MinElevationStdDev)
            {
                break;
            }

            BiomeBalanceHelper.InjectRelief(plan, config.MinElevationStdDev, random);
        }
    }
}
