using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class IslandMaskStage
{
    private const uint StageSalt = 14;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        int width = plan.Width;
        int height = plan.Height;
        int cellCount = width * height;
        plan.IslandMask = new float[cellCount];

        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);

        float innerRadius = config.MaskInnerRadius;
        float outerRadius = config.MaskOuterRadius > 0f ? config.MaskOuterRadius : config.MainIslandRadius;
        int border = Math.Max(0, config.MinOceanBorderCells);

        var satelliteCenters = new List<(float X, float Y, float Radius)>();
        int satelliteCount = Math.Clamp(config.SatelliteIslandCount, 0, 12);

        for (int i = 0; i < satelliteCount; i++)
        {
            float angle = random.NextFloat() * MathF.PI * 2f;
            float orbit = outerRadius * (0.62f + random.NextFloat() * 0.22f);
            float sx = centerX + MathF.Cos(angle) * maxRadius * orbit;
            float sy = centerY + MathF.Sin(angle) * maxRadius * orbit;
            float radius = maxRadius * (config.SatelliteMinRadius +
                                        random.NextFloat() * (config.SatelliteMaxRadius - config.SatelliteMinRadius));
            satelliteCenters.Add((sx, sy, radius));
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = x / (float)Math.Max(1, width - 1);
                float ny = y / (float)Math.Max(1, height - 1);

                (float wx, float wy) = NoiseUtility.DomainWarp(
                    stageSeed,
                    nx,
                    ny,
                    config.WarpLargeStrength,
                    config.WarpMediumStrength,
                    config.WarpSmallStrength);

                float sampleX = wx * (width - 1);
                float sampleY = wy * (height - 1);
                float dx = (sampleX - centerX) / maxRadius;
                float dy = (sampleY - centerY) / maxRadius;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                float largeNoise = NoiseUtility.Fbm(stageSeed + 10, wx * 2.2f, wy * 2.2f, octaves: 4);
                float mediumNoise = NoiseUtility.Fbm(stageSeed + 11, wx * 5.5f, wy * 5.5f, octaves: 3);
                float fineNoise = NoiseUtility.Fbm(stageSeed + 12, wx * 16f, wy * 16f, octaves: 2);

                float sdf = outerRadius - dist;
                sdf += (largeNoise - 0.5f) * config.MaskNoiseLarge;
                sdf += (mediumNoise - 0.5f) * config.MaskNoiseMedium;
                sdf += (fineNoise - 0.5f) * config.MaskNoiseFine;

                float coastWidth = MathF.Max(0.04f, outerRadius - innerRadius);
                float mask = NoiseUtility.SmoothStep(-coastWidth * 0.35f, coastWidth * 0.55f, sdf);

                foreach ((float sx, float sy, float radius) in satelliteCenters)
                {
                    float sdx = x - sx;
                    float sdy = y - sy;
                    float sdist = MathF.Sqrt(sdx * sdx + sdy * sdy) / radius;
                    float satSdf = 1f - sdist;
                    satSdf += (NoiseUtility.Fbm(stageSeed + 13, x * 0.06f, y * 0.06f, octaves: 2) - 0.5f) * 0.25f;
                    float satMask = NoiseUtility.SmoothStep(-0.15f, 0.35f, satSdf);
                    mask = MathF.Max(mask, satMask * 0.9f);
                }

                if (x < border || y < border || x >= width - border || y >= height - border)
                {
                    mask *= 0.15f;
                }

                plan.IslandMask[y * width + x] = Math.Clamp(mask, 0f, 1.25f);
            }
        }
    }
}
