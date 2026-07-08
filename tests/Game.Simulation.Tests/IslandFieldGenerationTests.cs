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
        PaddockCount = 1
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
}
