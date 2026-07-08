using Game.Content.Definitions;
using Game.Generation.Island.Stages;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandMaskStageTests
{
    [Fact]
    public void IslandMaskStage_CenterHasHigherLandTendencyThanEdge()
    {
        var config = new IslandDefinition
        {
            MaskInnerRadius = 0.55f,
            MaskOuterRadius = 1.05f,
            MainIslandRadius = 1.05f,
            SatelliteIslandCount = 2,
            MinOceanBorderCells = 2
        };
        var plan = new IslandPlan(64, 64, seed: 1234UL);

        IslandMaskStage.Execute(plan, config, seed: 1234UL);

        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float centerMask = plan.IslandMask[(int)centerY * plan.Width + (int)centerX];

        int edgeX = 4;
        int edgeY = 4;
        float edgeMask = plan.IslandMask[edgeY * plan.Width + edgeX];

        Assert.True(centerMask > edgeMask);
    }

    [Fact]
    public void IslandMaskStage_IsDeterministic()
    {
        var config = TestSaveDefaults.Island;
        var planA = new IslandPlan(32, 32, seed: 555UL);
        var planB = new IslandPlan(32, 32, seed: 555UL);

        IslandMaskStage.Execute(planA, config, seed: 555UL);
        IslandMaskStage.Execute(planB, config, seed: 555UL);

        Assert.Equal(planA.IslandMask, planB.IslandMask);
    }
}
