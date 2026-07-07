using Game.Generation.Island;
using Game.Generation.WorldGen;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandBalanceTests
{
    [Fact]
    public void Generate_KeepsWetBiomeShareWithinConfiguredCap()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(128, 128, 9101UL);

        float wetShare = BiomeBalanceHelper.MeasureWetBiomeShare(plan);
        Assert.True(wetShare <= TestSaveDefaults.FullIsland.MaxWetBiomeShare + 0.02f);
    }

    [Fact]
    public void Generate_MeetsMinimumElevationVariance()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(128, 128, 9102UL);

        float stdDev = BiomeBalanceHelper.MeasureLandElevationStdDev(plan);
        Assert.True(stdDev >= TestSaveDefaults.FullIsland.MinElevationStdDev * 0.85f);
    }

    [Fact]
    public void BalancePass_IsDeterministicForSeed()
    {
        var planner = new IslandPlanner(TestSaveDefaults.FullIsland);
        IslandPlan first = planner.Generate(128, 128, 9103UL);
        IslandPlan second = planner.Generate(128, 128, 9103UL);

        Assert.Equal(
            BiomeBalanceHelper.MeasureWetBiomeShare(first),
            BiomeBalanceHelper.MeasureWetBiomeShare(second),
            precision: 4);
        Assert.Equal(
            BiomeBalanceHelper.MeasureLandElevationStdDev(first),
            BiomeBalanceHelper.MeasureLandElevationStdDev(second),
            precision: 4);
    }
}
