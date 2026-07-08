using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Generation.Voronoi;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class VoronoiFieldTests
{
    [Fact]
    public void VoronoiField_F2IsGreaterOrEqualToF1()
    {
        var plan = CreatePlanWithRegions(32, 32, 8);
        var config = new IslandDefinition { BiomeBlendNeighborCount = 3 };

        VoronoiField.ComputeField(plan, config, seed: 42UL);

        for (int i = 0; i < plan.VoronoiF1.Length; i++)
        {
            Assert.True(plan.VoronoiF2[i] >= plan.VoronoiF1[i] - 0.0001f);
            Assert.True(plan.VoronoiEdge[i] >= 0f);
        }
    }

    [Fact]
    public void VoronoiField_WarpChangesFieldDeterministically()
    {
        var planA = CreatePlanWithRegions(24, 24, 6);
        var planB = CreatePlanWithRegions(24, 24, 6);
        var lowWarp = new IslandDefinition { WarpLargeStrength = 0.01f, WarpMediumStrength = 0.01f, WarpSmallStrength = 0.01f };
        var highWarp = new IslandDefinition { WarpLargeStrength = 0.25f, WarpMediumStrength = 0.12f, WarpSmallStrength = 0.05f };

        VoronoiField.ComputeField(planA, lowWarp, seed: 99UL);
        VoronoiField.ComputeField(planB, highWarp, seed: 99UL);

        int differentEdges = 0;
        for (int i = 0; i < planA.VoronoiEdge.Length; i++)
        {
            if (MathF.Abs(planA.VoronoiEdge[i] - planB.VoronoiEdge[i]) > 0.001f)
            {
                differentEdges++;
            }
        }

        Assert.True(differentEdges > 0);
    }

    [Fact]
    public void VoronoiField_BlendWeightsSumToOne()
    {
        var plan = CreatePlanWithRegions(16, 16, 4);
        var config = new IslandDefinition { BiomeBlendNeighborCount = 3, BiomeBlendPower = 2f };

        VoronoiField.ComputeField(plan, config, seed: 7UL);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                int blendBase = index * config.BiomeBlendNeighborCount;
                float sum = 0f;
                for (int i = 0; i < config.BiomeBlendNeighborCount; i++)
                {
                    sum += plan.VoronoiBlendWeights[blendBase + i];
                }

                Assert.InRange(sum, 0.99f, 1.01f);
            }
        }
    }

    [Fact]
    public void NoiseUtility_SmoothStep_InterpolatesEndpoints()
    {
        Assert.Equal(0f, NoiseUtility.SmoothStep(0f, 1f, 0f), 3);
        Assert.Equal(1f, NoiseUtility.SmoothStep(0f, 1f, 1f), 3);
        Assert.InRange(NoiseUtility.SmoothStep(0f, 1f, 0.5f), 0.4f, 0.6f);
    }

    private static IslandPlan CreatePlanWithRegions(int width, int height, int regionCount)
    {
        var plan = new IslandPlan(width, height, seed: 1UL);
        for (int i = 0; i < regionCount; i++)
        {
            plan.Regions.Add(new IslandRegion
            {
                Id = i,
                SiteX = (i * 17 + 3) % width,
                SiteY = (i * 13 + 5) % height
            });
        }

        return plan;
    }
}
