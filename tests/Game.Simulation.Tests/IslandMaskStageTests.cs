using Game.Content.Definitions;
using Game.Generation.Island;
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
            UseLegacyIslandMask = true,
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
    public void IslandGenerator_DoesNotProduceLongStraightBorderCoastlines()
    {
        var config = new IslandDefinition
        {
            UseLegacyIslandMask = true,
            OverworldSize = 128,
            RegionCount = 24,
            MainIslandRadius = 0.72f,
            MaskOuterRadius = 0.72f,
            MaskInnerRadius = 0.25f,
            MaskNoiseLarge = 0.42f,
            MaskNoiseMedium = 0.20f,
            MaskNoiseFine = 0.08f,
            HeightMaskWeight = 0.55f,
            LandElevationThreshold = 0.18f,
            MinOceanBorderCells = 12,
            SatelliteIslandCount = 2,
            VolcanicConeCount = 1,
            RiverCount = 2
        };

        var plan = new IslandPlanner(config).Generate(128, 128, seed: 4242UL);

        int maxHorizontalRun = FindLongestStraightCoastRun(plan, horizontal: true);
        int maxVerticalRun = FindLongestStraightCoastRun(plan, horizontal: false);

        Assert.True(
            maxHorizontalRun < plan.Width * 0.35,
            $"Horizontal straight coast run was {maxHorizontalRun} cells.");
        Assert.True(
            maxVerticalRun < plan.Height * 0.35,
            $"Vertical straight coast run was {maxVerticalRun} cells.");
    }

    private static int FindLongestStraightCoastRun(IslandPlan plan, bool horizontal)
    {
        int maxRun = 0;

        if (horizontal)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                int run = 0;
                for (int x = 0; x < plan.Width; x++)
                {
                    if (plan.GetCell(x, y).IsCoast)
                    {
                        run++;
                        maxRun = Math.Max(maxRun, run);
                    }
                    else
                    {
                        run = 0;
                    }
                }
            }
        }
        else
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int run = 0;
                for (int y = 0; y < plan.Height; y++)
                {
                    if (plan.GetCell(x, y).IsCoast)
                    {
                        run++;
                        maxRun = Math.Max(maxRun, run);
                    }
                    else
                    {
                        run = 0;
                    }
                }
            }
        }

        return maxRun;
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
