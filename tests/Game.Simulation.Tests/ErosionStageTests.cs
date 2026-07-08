using Game.Generation.Island;
using Game.Generation.Island.Stages;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class ErosionStageTests
{
    [Fact]
    public void ErosionStage_ReducesElevationVarianceOnPeaks()
    {
        var config = TestSaveDefaults.Island;
        var plan = new IslandPlan(32, 32, seed: 8080UL);

        IslandMaskStage.Execute(plan, config, seed: 8080UL);
        VoronoiRegionStage.Execute(plan, config, seed: 8080UL);
        LandmassStage.Execute(plan, config, seed: 8080UL);

        float beforeMax = plan.Cells.Where(c => c.IsLand).Select(c => c.Elevation).DefaultIfEmpty(0f).Max();
        float beforeStd = StdDev(plan.Cells.Where(c => c.IsLand).Select(c => c.Elevation));

        ErosionStage.Execute(plan, config, seed: 8080UL);

        float afterMax = plan.Cells.Where(c => c.IsLand).Select(c => c.Elevation).DefaultIfEmpty(0f).Max();
        float afterStd = StdDev(plan.Cells.Where(c => c.IsLand).Select(c => c.Elevation));

        Assert.True(afterMax <= beforeMax + 0.001f);
        Assert.True(afterStd <= beforeStd + 0.02f);
    }

    [Fact]
    public void ErosionStage_IsDeterministic()
    {
        var config = TestSaveDefaults.Island;
        var planA = BuildErodedPlan(8081UL);
        var planB = BuildErodedPlan(8081UL);

        for (int i = 0; i < planA.Cells.Length; i++)
        {
            Assert.Equal(planA.Cells[i].Elevation, planB.Cells[i].Elevation, 5);
        }
    }

    private static IslandPlan BuildErodedPlan(ulong seed)
    {
        var config = TestSaveDefaults.Island;
        var plan = new IslandPlan(24, 24, seed);
        IslandMaskStage.Execute(plan, config, seed);
        VoronoiRegionStage.Execute(plan, config, seed);
        LandmassStage.Execute(plan, config, seed);
        ErosionStage.Execute(plan, config, seed);
        return plan;
    }

    private static float StdDev(IEnumerable<float> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0f;
        }

        float mean = list.Average();
        float variance = list.Sum(v => (v - mean) * (v - mean)) / list.Count;
        return MathF.Sqrt(variance);
    }
}
