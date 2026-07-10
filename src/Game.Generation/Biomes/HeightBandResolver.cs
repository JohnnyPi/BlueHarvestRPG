using Game.Content.Definitions;

namespace Game.Generation.Biomes;

public enum HighlandBand
{
    Foothills,
    Hills,
    SmallMountains,
    Mountains
}

public static class HeightBandResolver
{
    public static HighlandBand Resolve(float elevation, BiomeRulesDefinition rules)
    {
        if (elevation >= rules.MountainsMinElevation)
        {
            return HighlandBand.Mountains;
        }

        if (elevation >= rules.SmallMountainMinElevation)
        {
            return HighlandBand.SmallMountains;
        }

        if (elevation >= rules.HillsMinElevation)
        {
            return HighlandBand.Hills;
        }

        if (elevation >= rules.FoothillsMinElevation)
        {
            return HighlandBand.Foothills;
        }

        return HighlandBand.Foothills;
    }
}
