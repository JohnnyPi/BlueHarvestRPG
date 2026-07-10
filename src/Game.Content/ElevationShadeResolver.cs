using Game.Content.Definitions;
using Game.Simulation.World;

namespace Game.Content;

public static class ElevationShadeResolver
{
    private static readonly string[] BiomeNames = Enum.GetNames<BiomeId>();

    public static float ResolveDarkening(
        BiomeId biome,
        float elevation,
        ElevationShadingDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        string biomeName = (uint)biome < (uint)BiomeNames.Length
            ? BiomeNames[(int)biome]
            : biome.ToString();

        if (!definition.Enabled ||
            !float.IsFinite(elevation) ||
            definition.ExcludedBiomes.Contains(biomeName, StringComparer.OrdinalIgnoreCase))
        {
            return 0f;
        }

        ElevationShadeProfileDefinition profile =
            definition.Biomes.TryGetValue(biomeName, out ElevationShadeProfileDefinition? biomeProfile)
                ? biomeProfile
                : definition.DefaultProfile;

        if (!profile.Enabled || profile.MaxDarkening <= 0f)
        {
            return 0f;
        }

        float range = profile.FullBrightnessElevation - profile.DarkestElevation;
        if (range <= 0f)
        {
            return elevation < profile.FullBrightnessElevation
                ? Math.Clamp(profile.MaxDarkening, 0f, 1f)
                : 0f;
        }

        float normalizedElevation = Math.Clamp(
            (elevation - profile.DarkestElevation) / range,
            0f,
            1f);
        return Math.Clamp(profile.MaxDarkening, 0f, 1f) * (1f - normalizedElevation);
    }

    public static float ResolveBrightness(
        BiomeId biome,
        float elevation,
        ElevationShadingDefinition definition)
    {
        return 1f - ResolveDarkening(biome, elevation, definition);
    }
}
