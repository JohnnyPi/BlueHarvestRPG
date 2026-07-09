using Game.Content.Definitions;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class SaveLoadDeterminismTests
{
    [Fact]
    public void LoadPath_MatchesNewGameGenerator_ForSameSeed()
    {
        const ulong seed = 4242UL;
        var island = TestSaveDefaults.Island;
        var biomeRules = new BiomeRulesDefinition { ForestMinMoisture = 0.35f };
        StructureBlueprintCatalog catalog = TestSaveDefaults.BlueprintCatalog;

        Overworld newGameWorld = new IslandWorldGenerator(island, catalog, biomeRules).Generate(seed);
        Overworld loadPathWorld = new IslandWorldGenerator(island, catalog, biomeRules).Generate(seed);

        Assert.NotNull(newGameWorld.IslandPlan);
        Assert.NotNull(loadPathWorld.IslandPlan);
        AssertIslandPlansEqual(newGameWorld.IslandPlan!, loadPathWorld.IslandPlan!);

        for (int y = 0; y < newGameWorld.Height; y++)
        {
            for (int x = 0; x < newGameWorld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                WorldCell newGameCell = newGameWorld.GetCellValue(coord);
                WorldCell loadPathCell = loadPathWorld.GetCellValue(coord);
                Assert.Equal(newGameCell.Biome, loadPathCell.Biome);
                Assert.Equal(newGameCell.Elevation, loadPathCell.Elevation, precision: 5);
            }
        }
    }

    [Fact]
    public void LoadPath_WithoutContentInputs_DivergesFromNewGamePath()
    {
        const ulong seed = 4242UL;
        var island = TestSaveDefaults.FullIsland;
        var biomeRules = new BiomeRulesDefinition
        {
            MountainsMinElevation = 0.60f,
            HillsMinElevation = 0.50f,
        };
        StructureBlueprintCatalog catalog = TestSaveDefaults.BlueprintCatalog;

        Overworld newGameWorld = new IslandWorldGenerator(island, catalog, biomeRules).Generate(seed);
        Overworld legacyLoadWorld = new IslandWorldGenerator(island).Generate(seed);

        int biomeDifferences = 0;
        for (int y = 0; y < newGameWorld.Height; y++)
        {
            for (int x = 0; x < newGameWorld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (newGameWorld.GetCellValue(coord).Biome != legacyLoadWorld.GetCellValue(coord).Biome)
                {
                    biomeDifferences++;
                }
            }
        }

        Assert.True(biomeDifferences > 0, "Expected mismatched generator inputs to produce different biomes.");
    }

    private static void AssertIslandPlansEqual(IslandPlan first, IslandPlan second)
    {
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);

        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                ref IslandCellData a = ref first.GetCell(x, y);
                ref IslandCellData b = ref second.GetCell(x, y);
                Assert.Equal(a.Biome, b.Biome);
                Assert.Equal(a.Elevation, b.Elevation, precision: 5);
                Assert.Equal(a.Role, b.Role);
            }
        }
    }
}
