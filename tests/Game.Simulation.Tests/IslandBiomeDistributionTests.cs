using Game.Generation.Island;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation.Coordinates;
using Game.Simulation.Session;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandBiomeDistributionTests
{
    [Fact]
    public void FullIsland_ContainsEachMajorBiome()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.FullIsland);
        Overworld world = generator.Generate(777UL);

        var present = new HashSet<BiomeId>();
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                present.Add(world.GetCellValue(new WorldCoord(x, y)).Biome);
            }
        }

        Assert.Contains(BiomeId.Ocean, present);
        Assert.Contains(BiomeId.Beach, present);
        Assert.Contains(BiomeId.Plains, present);
        Assert.Contains(BiomeId.Forest, present);
        Assert.Contains(BiomeId.Jungle, present);
        Assert.Contains(BiomeId.Hills, present);
    }

    [Fact]
    public void FullIsland_NoSingleLandBiomeDominates()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.FullIsland);
        Overworld world = generator.Generate(888UL);

        var counts = new Dictionary<BiomeId, int>();
        int landCells = 0;

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                BiomeId biome = world.GetCellValue(new WorldCoord(x, y)).Biome;
                if (biome is BiomeId.Ocean or BiomeId.Beach)
                {
                    continue;
                }

                landCells++;
                counts.TryGetValue(biome, out int current);
                counts[biome] = current + 1;
            }
        }

        Assert.True(landCells > 0);
        foreach ((BiomeId biome, int count) in counts)
        {
            float share = count / (float)landCells;
            Assert.True(share <= 0.45f, $"{biome} occupied {share:P0} of land.");
        }
    }

    [Fact]
    public void GeneratedIsland_HasSubstantialLandmass()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.Island).Generate(64, 64, 4242UL);
        int landCells = plan.Cells.Count(cell => cell.IsLand);
        int oceanCells = plan.Cells.Count(cell => !cell.IsLand);

        Assert.True(landCells > 0);
        Assert.True(oceanCells > 0);
        Assert.True(landCells > oceanCells);
    }

    [Fact]
    public void VisitorCenterSpawn_IsPlainsClearing()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.FullIsland);
        Overworld world = generator.Generate(1234UL);
        IslandPlan plan = world.IslandPlan!;

        Assert.True(plan.VisitorCenterCell.X >= 0);
        BiomeId visitorBiome = plan.GetCell(plan.VisitorCenterCell).Biome;
        Assert.Equal(BiomeId.Plains, visitorBiome);
    }

    [Fact]
    public void FacilityLandmarks_UsePlainsOrBeachBiomes()
    {
        IslandPlan plan = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(1234UL).IslandPlan!;

        foreach ((IslandCellRole role, string _) in new (IslandCellRole, string)[]
                 {
                     (IslandCellRole.Hotel, "Hotel"),
                     (IslandCellRole.Maintenance, "Maintenance"),
                     (IslandCellRole.Dock, "Dock")
                 })
        {
            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    if (!plan.GetCell(x, y).Role.HasFlag(role))
                    {
                        continue;
                    }

                    BiomeId biome = plan.GetCell(x, y).Biome;
                    Assert.True(
                        biome is BiomeId.Plains or BiomeId.Beach,
                        $"{role} at ({x},{y}) is {biome}.");
                }
            }
        }
    }
}
