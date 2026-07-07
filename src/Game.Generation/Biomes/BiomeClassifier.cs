using Game.Content.Definitions;
using Game.Simulation.World;

namespace Game.Generation.Biomes;

public sealed class BiomeClassifier
{
    private readonly BiomeRulesDefinition _rules;

    public BiomeClassifier(BiomeRulesDefinition rules)
    {
        _rules = rules;
    }

    public BiomeId Classify(float elevation, float moisture, float temperature)
    {
        _ = temperature;

        if (elevation < _rules.OceanMaxElevation)
        {
            return BiomeId.Ocean;
        }

        if (elevation < _rules.BeachMaxElevation)
        {
            return BiomeId.Beach;
        }

        if (elevation > _rules.MountainsMinElevation)
        {
            return BiomeId.Mountains;
        }

        if (elevation > _rules.HillsMinElevation)
        {
            return BiomeId.Hills;
        }

        if (moisture > _rules.SwampMinMoisture)
        {
            return BiomeId.Swamp;
        }

        if (moisture > _rules.ForestMinMoisture)
        {
            return BiomeId.Forest;
        }

        return BiomeId.Plains;
    }

    public static BiomeClassifier CreateDefault()
    {
        return new BiomeClassifier(new BiomeRulesDefinition());
    }
}
