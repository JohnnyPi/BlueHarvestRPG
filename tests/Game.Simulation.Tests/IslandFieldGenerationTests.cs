using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Island.Stages;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandFieldGenerationTests
{
    private static IslandDefinition CreateNublarConfig() => new()
    {
        OverworldSize = 128,
        RegionCount = 24,
        LandElevationThreshold = 0.35f,
        SeaLevel = 0.35f,
        MinOceanBorderCells = 8,
        MinLandComponentCells = 9,
        VolcanicConeCount = 1,
        RiverCount = 2,
        DockCount = 1,
        HelipadCount = 1,
        HotelCount = 1,
        RestaurantCount = 1,
        AttractionCount = 1,
        MaintenanceAreaCount = 1,
        RuinCount = 1,
        FortificationCount = 1,
        PaddockCount = 1,
        OceanFrame = new OceanFrameDefinition
        {
            OverscanScale = 1.2f,
            MinLandDistanceFromEdge = 10,
            MinCoastDistanceFromEdge = 6,
            MaxRegenerationAttempts = 4,
            MaxAxisAlignedCoastRun = 24,
            EdgeLinearityBand = 16,
        }
    };

    [Fact]
    public void IslandMaskStage_NublarProducesLandmass()
    {
        var config = CreateNublarConfig();
        var plan = new IslandPlan(128, 128, seed: 4242UL);

        IslandMaskStage.Execute(plan, config, seed: 4242UL);

        int landishCells = plan.IslandMask.Count(mask => mask > config.IslandShape.LandThreshold);
        Assert.True(landishCells > 2000, $"Expected substantial mask land, got {landishCells}.");
    }

    [Fact]
    public void CoastDistanceStage_SignsCorrect()
    {
        var config = CreateNublarConfig();
        var plan = new IslandPlan(128, 128, seed: 4242UL);
        IslandMaskStage.Execute(plan, config, seed: 4242UL);
        CoastDistanceStage.Execute(plan, config);

        bool hasInland = false;
        bool hasOcean = false;
        bool hasShore = false;

        for (int i = 0; i < plan.CoastDistance.Length; i++)
        {
            if (plan.CoastDistance[i] > 0.05f)
            {
                hasInland = true;
            }
            else if (plan.CoastDistance[i] < -0.01f)
            {
                hasOcean = true;
            }
            else
            {
                hasShore = true;
            }
        }

        Assert.True(hasInland);
        Assert.True(hasOcean);
        Assert.True(hasShore);
    }

    [Fact]
    public void IslandGenerator_HasShallowWaterShelf()
    {
        IslandPlan plan = new IslandPlanner(CreateNublarConfig()).Generate(128, 128, seed: 4242UL);

        int shallow = plan.Cells.Count(cell => cell.Biome == BiomeId.ShallowWater);
        int reef = plan.Cells.Count(cell => cell.Biome == BiomeId.Reef);
        int ocean = plan.Cells.Count(cell => cell.Biome == BiomeId.Ocean);

        Assert.True(shallow > 0, "Expected shallow water shelf cells.");
        Assert.True(ocean > 0, "Expected deep ocean cells.");
        Assert.True(shallow + reef > 0);
    }

    [Fact]
    public void CoastalWidthStage_ProducesDeterministicSharedSmoothedVariation()
    {
        var config = CreateNublarConfig();
        var first = CreateCoastalFieldPlan();
        var second = CreateCoastalFieldPlan();

        CoastalWidthStage.Execute(first, config, seed: 4242UL);
        CoastalWidthStage.Execute(second, config, seed: 4242UL);

        Assert.Equal(first.CoastalWidthVariation, second.CoastalWidthVariation);
        Assert.Equal(first.BeachWidth, second.BeachWidth);
        Assert.Equal(first.ShallowWaterWidth, second.ShallowWaterWidth);
        Assert.True(first.CoastalWidthVariation.Distinct().Count() > 8);

        float maxNeighborDelta = 0f;
        float maxShallowBlendDelta = 0f;
        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                int index = y * first.Width + x;
                float blend = first.CoastalWidthVariation[index];
                float expectedBeach = config.MinBeachCoastDistance
                    + (config.MaxBeachCoastDistance - config.MinBeachCoastDistance) * blend;
                Assert.Equal(expectedBeach, first.BeachWidth[index], precision: 6);
                float shallowBlend =
                    (first.ShallowWaterWidth[index] - config.MinShallowWaterCoastDistance)
                    / (config.MaxShallowWaterCoastDistance - config.MinShallowWaterCoastDistance);
                float shallowBlendDelta = MathF.Abs(shallowBlend - blend);
                Assert.InRange(shallowBlendDelta, 0f, 0.151f);
                maxShallowBlendDelta = MathF.Max(maxShallowBlendDelta, shallowBlendDelta);

                if (x + 1 < first.Width)
                {
                    maxNeighborDelta = MathF.Max(
                        maxNeighborDelta,
                        MathF.Abs(blend - first.CoastalWidthVariation[index + 1]));
                }

                if (y + 1 < first.Height)
                {
                    maxNeighborDelta = MathF.Max(
                        maxNeighborDelta,
                        MathF.Abs(blend - first.CoastalWidthVariation[index + first.Width]));
                }
            }
        }

        Assert.True(maxNeighborDelta < 0.12f, $"Adjacent coastal variation jumped by {maxNeighborDelta:0.###}.");
        Assert.True(maxShallowBlendDelta > 0.001f, "Shallow water should retain independent secondary variation.");
    }

    [Fact]
    public void IslandGenerator_ShallowWaterUsesSharedLocalWidth()
    {
        var config = CreateNublarConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 4242UL);

        var shallowIndices = Enumerable.Range(0, plan.Cells.Length)
            .Where(index => plan.Cells[index].Biome == BiomeId.ShallowWater && plan.CoastDistance[index] <= 0f)
            .ToList();

        Assert.NotEmpty(shallowIndices);
        Assert.All(shallowIndices, index =>
            Assert.True(-plan.CoastDistance[index] <= plan.ShallowWaterWidth[index] + 0.0001f));
        Assert.True(plan.ShallowWaterWidth.Select(width => MathF.Round(width, 4)).Distinct().Count() >= 3);
        Assert.True(plan.GenerationDiagnostics.MaxObservedBeachWidth
            > plan.GenerationDiagnostics.MinObservedBeachWidth);
        Assert.True(plan.GenerationDiagnostics.MaxObservedShallowWaterWidth
            > plan.GenerationDiagnostics.MinObservedShallowWaterWidth);
    }

    [Fact]
    public void IslandGenerator_RidgesRaiseElevation()
    {
        var config = CreateNublarConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 9001UL);

        float ridgeSample = 0f;
        foreach (IslandRidgeDefinition ridge in config.Ridges)
        {
            if (ridge.Points.Length == 0)
            {
                continue;
            }

            float px = ridge.Points[0][0];
            float py = ridge.Points[0][1];
            int x = (int)MathF.Round((px + 1f) * 0.5f * (plan.Width - 1));
            int y = (int)MathF.Round((py + 1f) * 0.5f * (plan.Height - 1));
            x = Math.Clamp(x, 0, plan.Width - 1);
            y = Math.Clamp(y, 0, plan.Height - 1);
            ridgeSample = MathF.Max(ridgeSample, plan.GetCell(x, y).Elevation);
        }

        float interiorAverage = 0f;
        int interiorCount = 0;
        for (int i = 0; i < plan.CoastDistance.Length; i++)
        {
            if (plan.CoastDistance[i] < 0.12f)
            {
                continue;
            }

            interiorAverage += plan.Cells[i].Elevation;
            interiorCount++;
        }

        float average = interiorCount > 0 ? interiorAverage / interiorCount : 0f;
        Assert.True(ridgeSample >= average - 0.05f);
    }

    [Fact]
    public void IslandGenerator_ParkFeaturesPreserved()
    {
        IslandPlan plan = new IslandPlanner(CreateNublarConfig()).Generate(128, 128, seed: 4242UL);

        Assert.Contains(plan.Structures, structure => structure.Type == StructureType.VisitorCenter);
        Assert.Contains(plan.Structures, structure => structure.Type == StructureType.Dock);
        Assert.True(plan.Cells.Count(cell => cell.IsLand) > 1000);
    }

    private static IslandPlan CreateCoastalFieldPlan()
    {
        const int size = 32;
        var plan = new IslandPlan(size, size, seed: 4242UL)
        {
            CoastDistance = new float[size * size],
            Concavity = new float[size * size],
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                plan.CoastDistance[index] = (x - size / 2) / (float)size;
                plan.Concavity[index] = MathF.Sin(y * 0.2f) * 0.4f;
            }
        }

        return plan;
    }
}
