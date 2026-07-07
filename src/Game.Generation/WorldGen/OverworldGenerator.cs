using Game.Generation.Biomes;
using Game.Generation.Noise;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Generation.WorldGen;

public sealed class OverworldGenerator
{
    private readonly BiomeClassifier _biomeClassifier;

    public OverworldGenerator(BiomeClassifier biomeClassifier)
    {
        _biomeClassifier = biomeClassifier;
    }

    public OverworldGenerator()
        : this(BiomeClassifier.CreateDefault())
    {
    }

    public Overworld Generate(int width, int height, ulong seed)
    {
        var world = new Overworld(width, height, seed);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)(width - 1);
                float ny = y / (float)(height - 1);

                float elevationNoise =
                    ValueNoise.Sample(seed, nx * 3f, ny * 3f, octaves: 4) * 0.65f +
                    ValueNoise.Sample(seed + 1, nx * 8f, ny * 8f, octaves: 3) * 0.35f;

                float moisture = ValueNoise.Sample(seed + 2, nx * 4f, ny * 4f, octaves: 4);
                float latitude = 1f - MathF.Abs(ny * 2f - 1f);
                float temperature = latitude * 0.6f + ValueNoise.Sample(seed + 3, nx * 5f, ny * 5f) * 0.4f;

                float falloff = CalculateIslandFalloff(x, y, width, height);
                float elevation = elevationNoise * falloff;

                ref WorldCell cell = ref world.GetCell(new WorldCoord(x, y));
                cell.Elevation = elevation;
                cell.Moisture = moisture;
                cell.Temperature = Math.Clamp(temperature - elevation * 0.25f, 0f, 1f);
                cell.Biome = _biomeClassifier.Classify(cell.Elevation, cell.Moisture, cell.Temperature);
                cell.HasLocalChanges = false;
            }
        }

        RegionalFeatureGraph.ApplyRoads(world);

        return world;
    }

    private static float CalculateIslandFalloff(int x, int y, int width, int height)
    {
        float nx = (x / (float)(width - 1)) * 2f - 1f;
        float ny = (y / (float)(height - 1)) * 2f - 1f;

        float distance = MathF.Sqrt(nx * nx + ny * ny);
        return Math.Clamp(1f - distance, 0f, 1f);
    }
}
