using Game.Content.Definitions;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class RegionBiomeStageTests
{
    [Fact]
    public void DifferentBiomeRules_ProduceDifferentBiomeLayouts()
    {
        const ulong seed = 4242UL;
        var island = TestSaveDefaults.FullIsland;

        var defaultRules = new BiomeRulesDefinition();
        var alternateRules = new BiomeRulesDefinition
        {
            MountainsMinElevation = 0.60f,
            HillsMinElevation = 0.50f,
            SwampMinMoisture = 0.20f,
            ForestMinMoisture = 0.20f,
        };

        Overworld defaultWorld = new IslandWorldGenerator(island, biomeRules: defaultRules).Generate(seed);
        Overworld alternateWorld = new IslandWorldGenerator(island, biomeRules: alternateRules).Generate(seed);

        int differences = 0;
        for (int y = 0; y < defaultWorld.Height; y++)
        {
            for (int x = 0; x < defaultWorld.Width; x++)
            {
                BiomeId defaultBiome = defaultWorld.GetCellValue(new WorldCoord(x, y)).Biome;
                BiomeId alternateBiome = alternateWorld.GetCellValue(new WorldCoord(x, y)).Biome;
                if (defaultBiome != alternateBiome)
                {
                    differences++;
                }
            }
        }

        Assert.True(differences > 0, "Expected biome rules to change the generated island layout.");
    }

    [Fact]
    public void SameBiomeRules_AndSeed_ProduceIdenticalBiomeLayouts()
    {
        const ulong seed = 4242UL;
        var island = TestSaveDefaults.FullIsland;
        var rules = new BiomeRulesDefinition { ForestMinMoisture = 0.35f };

        Overworld first = new IslandWorldGenerator(island, biomeRules: rules).Generate(seed);
        Overworld second = new IslandWorldGenerator(island, biomeRules: rules).Generate(seed);

        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                Assert.Equal(
                    first.GetCellValue(coord).Biome,
                    second.GetCellValue(coord).Biome);
            }
        }
    }
}
