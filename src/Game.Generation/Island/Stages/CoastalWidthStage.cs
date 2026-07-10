using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Builds one deterministic, low-frequency coastal variation field shared by
/// land-side beaches and ocean-side shallows.
/// </summary>
public static class CoastalWidthStage
{
    private const uint StageSalt = 24;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        int count = plan.Width * plan.Height;
        plan.CoastalWidthVariation = new float[count];
        plan.BeachWidth = new float[count];
        plan.ShallowWaterWidth = new float[count];

        if (config.UseLegacyIslandMask || plan.CoastDistance.Length != count)
        {
            return;
        }

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        float frequency = MathF.Max(0.1f, config.CoastalWidthVariationFrequency);
        float[] variation = plan.CoastalWidthVariation;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                float nx = x / (float)Math.Max(1, plan.Width - 1);
                float ny = y / (float)Math.Max(1, plan.Height - 1);
                float noise = NoiseUtility.Fbm(stageSeed, nx * frequency, ny * frequency, octaves: 3);
                float concavity = plan.Concavity.Length > index ? plan.Concavity[index] : 0f;
                float bayInfluence = Math.Clamp((concavity + 1f) * 0.5f, 0f, 1f);
                variation[index] = Math.Clamp(noise * 0.72f + bayInfluence * 0.28f, 0f, 1f);
            }
        }

        Smooth(variation, plan.Width, plan.Height, config.CoastalWidthSmoothingPasses);

        for (int i = 0; i < count; i++)
        {
            float blend = variation[i];
            plan.BeachWidth[i] = Lerp(config.MinBeachCoastDistance, config.MaxBeachCoastDistance, blend);
            int x = i % plan.Width;
            int y = i / plan.Width;
            float nx = x / (float)Math.Max(1, plan.Width - 1);
            float ny = y / (float)Math.Max(1, plan.Height - 1);
            float secondary = NoiseUtility.Fbm(
                stageSeed + 1,
                nx * frequency * 1.8f,
                ny * frequency * 1.8f,
                octaves: 2);
            float shallowBlend = Math.Clamp(blend * 0.85f + secondary * 0.15f, 0f, 1f);
            plan.ShallowWaterWidth[i] = Lerp(
                config.MinShallowWaterCoastDistance,
                config.MaxShallowWaterCoastDistance,
                shallowBlend);
        }

        UpdateDiagnostics(plan, config);
    }

    private static void Smooth(float[] field, int width, int height, int requestedPasses)
    {
        int passes = Math.Clamp(requestedPasses, 0, 8);
        var scratch = new float[field.Length];

        for (int pass = 0; pass < passes; pass++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    int samples = 0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int sampleY = y + oy;
                        if (sampleY < 0 || sampleY >= height)
                        {
                            continue;
                        }

                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int sampleX = x + ox;
                            if (sampleX < 0 || sampleX >= width)
                            {
                                continue;
                            }

                            sum += field[sampleY * width + sampleX];
                            samples++;
                        }
                    }

                    scratch[y * width + x] = sum / samples;
                }
            }

            Array.Copy(scratch, field, field.Length);
        }
    }

    private static void UpdateDiagnostics(IslandPlan plan, IslandDefinition config)
    {
        var beachWidths = new List<float>();
        var shallowWidths = new List<float>();
        for (int i = 0; i < plan.CoastDistance.Length; i++)
        {
            float coastDistance = plan.CoastDistance[i];
            if (coastDistance >= 0f && coastDistance <= config.InlandCoastDistance)
            {
                beachWidths.Add(plan.BeachWidth[i]);
            }
            else if (coastDistance < 0f && -coastDistance <= config.ShelfWidth)
            {
                shallowWidths.Add(plan.ShallowWaterWidth[i]);
            }
        }

        IslandGenerationDiagnostics diagnostics = plan.GenerationDiagnostics;
        diagnostics.MinObservedBeachWidth = beachWidths.Count > 0 ? beachWidths.Min() : 0f;
        diagnostics.MaxObservedBeachWidth = beachWidths.Count > 0 ? beachWidths.Max() : 0f;
        diagnostics.MinObservedShallowWaterWidth = shallowWidths.Count > 0 ? shallowWidths.Min() : 0f;
        diagnostics.MaxObservedShallowWaterWidth = shallowWidths.Count > 0 ? shallowWidths.Max() : 0f;
    }

    private static float Lerp(float min, float max, float amount) => min + (max - min) * amount;
}
