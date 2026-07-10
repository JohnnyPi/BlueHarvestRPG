using Game.Content;
using Game.Content.Definitions;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class ElevationShadeResolverTests
{
    [Theory]
    [InlineData(0.1f, 0.24f)]
    [InlineData(0.35f, 0.24f)]
    [InlineData(0.625f, 0.12f)]
    [InlineData(0.9f, 0f)]
    [InlineData(1.0f, 0f)]
    public void ResolveDarkening_InterpolatesContinuously(float elevation, float expected)
    {
        var definition = new ElevationShadingDefinition();

        float darkening = ElevationShadeResolver.ResolveDarkening(
            BiomeId.Plains,
            elevation,
            definition);

        Assert.Equal(expected, darkening, precision: 3);
    }

    [Fact]
    public void ResolveDarkening_UsesBiomeProfile()
    {
        var definition = new ElevationShadingDefinition
        {
            Biomes =
            {
                [nameof(BiomeId.Beach)] = new ElevationShadeProfileDefinition
                {
                    DarkestElevation = 0.3f,
                    FullBrightnessElevation = 0.5f,
                    MaxDarkening = 0.1f
                }
            }
        };

        float beachDarkening = ElevationShadeResolver.ResolveDarkening(
            BiomeId.Beach,
            0.4f,
            definition);
        float plainsDarkening = ElevationShadeResolver.ResolveDarkening(
            BiomeId.Plains,
            0.4f,
            definition);

        Assert.Equal(0.05f, beachDarkening, precision: 3);
        Assert.True(plainsDarkening > beachDarkening);
    }

    [Theory]
    [InlineData(BiomeId.Ocean)]
    [InlineData(BiomeId.ShallowWater)]
    [InlineData(BiomeId.Reef)]
    public void ResolveDarkening_ExcludesWaterByDefault(BiomeId biome)
    {
        float darkening = ElevationShadeResolver.ResolveDarkening(
            biome,
            elevation: 0f,
            new ElevationShadingDefinition());

        Assert.Equal(0f, darkening);
    }

    [Fact]
    public void ResolveBrightness_ComplementsDarkening()
    {
        var definition = new ElevationShadingDefinition();

        float brightness = ElevationShadeResolver.ResolveBrightness(
            BiomeId.Forest,
            elevation: 0.35f,
            definition);

        Assert.Equal(0.76f, brightness, precision: 3);
    }
}
