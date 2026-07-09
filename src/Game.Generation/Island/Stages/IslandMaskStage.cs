using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Island.Fields;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class IslandMaskStage
{
    private const uint StageSalt = 14;

    public static void Execute(
        IslandPlan plan,
        IslandDefinition config,
        ulong seed,
        bool overscanGeneration = false,
        float shapeScale = 1f)
    {
        if (config.UseLegacyIslandMask)
        {
            ExecuteLegacy(plan, config, seed);
            return;
        }

        ExecuteBlobShape(plan, config, seed, overscanGeneration, shapeScale);
    }

    private static void ExecuteBlobShape(
        IslandPlan plan,
        IslandDefinition config,
        ulong seed,
        bool overscanGeneration,
        float shapeScale)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        int width = plan.Width;
        int height = plan.Height;
        int cellCount = width * height;
        plan.IslandMask = new float[cellCount];

        IslandShapeDefinition shape = config.IslandShape;
        int border = Math.Max(0, config.MinOceanBorderCells);
        float landThreshold = shape.LandThreshold;

        var satelliteBlobs = BuildSatelliteBlobs(config, random);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = x / (float)Math.Max(1, width - 1) * 2f - 1f;
                float py = y / (float)Math.Max(1, height - 1) * 2f - 1f;

                if (overscanGeneration && shapeScale < 0.999f)
                {
                    px /= shapeScale;
                    py /= shapeScale;
                }

                (float wx, float wy) = ApplyShapeDomainWarp(stageSeed, px, py, shape);

                float sdf = ShapeFieldComposer.EvaluateIslandSdf(
                    wx,
                    wy,
                    shape.AdditiveBlobs,
                    shape.SubtractiveBays,
                    shape.UnionSmoothness,
                    shape.SubtractSmoothness,
                    stageSeed);

                float shoreBand = shape.CoastlineDetail.Amplitude * 4f;
                if (MathF.Abs(sdf) < shoreBand)
                {
                    float detail = NoiseUtility.Fbm(
                        stageSeed + 30,
                        wx * shape.CoastlineDetail.Frequency,
                        wy * shape.CoastlineDetail.Frequency,
                        octaves: 2);
                    float ruggedness = NoiseUtility.Fbm(stageSeed + 70, wx * 0.5f, wy * 0.5f, octaves: 2);
                    float ruggednessScale = 0.3f + 1.4f * ruggedness;
                    float detailScale = shape.CoastlineDetail.PreserveLargeBays
                        ? NoiseUtility.SmoothStep(shoreBand, 0f, MathF.Abs(sdf))
                        : 1f;
                    // Bias toward erosion (subtract land) rather than symmetric growth.
                    float detailBias = detail - 0.55f;
                    sdf += detailBias * shape.CoastlineDetail.Amplitude * detailScale * ruggednessScale;
                }

                foreach (IslandBlobDefinition satellite in satelliteBlobs)
                {
                    float satSdf = EllipseSdf.Evaluate(wx, wy, satellite, stageSeed + 200);
                    sdf = ShapeFieldComposer.SmoothUnion(sdf, satSdf, satellite.Smoothness);
                }

                float nx = x / (float)Math.Max(1, width - 1);
                float ny = y / (float)Math.Max(1, height - 1);
                float edgeDist = Math.Min(
                    Math.Min(x, y),
                    Math.Min(width - 1 - x, height - 1 - y));
                float edgeBand = border * 3f;
                if (edgeDist < edgeBand)
                {
                    float edgeNoise = NoiseUtility.Fbm(stageSeed + 55, nx * 14f, ny * 14f, octaves: 3);
                    float edgeWobble = (edgeNoise - 0.5f) * 0.1f;
                    edgeWobble *= 1f - edgeDist / edgeBand;
                    sdf += edgeWobble;
                }

                float edgeFalloff = overscanGeneration
                    ? 1f
                    : IslandBorderUtility.ComputeEdgeFalloff(
                        x, y, width, height, border, stageSeed, nx, ny);
                float mask = NoiseUtility.SmoothStep(landThreshold - 0.03f, landThreshold + 0.05f, sdf) * edgeFalloff;

                plan.IslandMask[y * width + x] = Math.Clamp(mask, 0f, 1.25f);
            }
        }
    }

    private static List<IslandBlobDefinition> BuildSatelliteBlobs(IslandDefinition config, DeterministicRandom random)
    {
        var blobs = new List<IslandBlobDefinition>();
        int satelliteCount = Math.Clamp(config.SatelliteIslandCount, 0, 12);

        for (int i = 0; i < satelliteCount; i++)
        {
            float angle = random.NextFloat() * MathF.PI * 2f;
            float orbit = 0.62f + random.NextFloat() * 0.22f;
            float radius = config.SatelliteMinRadius +
                           random.NextFloat() * (config.SatelliteMaxRadius - config.SatelliteMinRadius);

            blobs.Add(new IslandBlobDefinition
            {
                Name = $"satellite_{i}",
                Center = [MathF.Cos(angle) * orbit, MathF.Sin(angle) * orbit],
                Radius = [radius, radius * (0.8f + random.NextFloat() * 0.4f)],
                Strength = 0.75f,
                Smoothness = 0.12f
            });
        }

        return blobs;
    }

    private static (float X, float Y) ApplyShapeDomainWarp(ulong stageSeed, float px, float py, IslandShapeDefinition shape)
    {
        IslandDomainWarpDefinition warp = shape.DomainWarp;

        (float wx, float wy) = NoiseUtility.LowFrequencyWarp(
            stageSeed,
            px,
            py,
            warp.LobingFrequency,
            warp.LobingAmplitude,
            warp.Octaves);

        (wx, wy) = NoiseUtility.LowFrequencyWarp(
            stageSeed + 20,
            wx,
            wy,
            warp.Frequency,
            warp.Amplitude,
            warp.Octaves);

        return NoiseUtility.DomainWarp(
            stageSeed + 40,
            wx,
            wy,
            warp.LargeStrength,
            warp.MediumStrength,
            warp.SmallStrength);
    }

    private static void ExecuteLegacy(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        int width = plan.Width;
        int height = plan.Height;
        int cellCount = width * height;
        plan.IslandMask = new float[cellCount];

        float mapCenterX = (width - 1) * 0.5f;
        float mapCenterY = (height - 1) * 0.5f;
        float maxRadius = Math.Min(mapCenterX, mapCenterY);

        float islandCenterX = mapCenterX + config.MainIslandCenterOffsetX * maxRadius;
        float islandCenterY = mapCenterY + config.MainIslandCenterOffsetY * maxRadius;

        float innerRadius = config.MaskInnerRadius;
        float outerRadius = config.MaskOuterRadius > 0f ? config.MaskOuterRadius : config.MainIslandRadius;
        int border = Math.Max(0, config.MinOceanBorderCells);
        float safeOuterRadius = IslandBorderUtility.ComputeSafeOuterRadius(
            outerRadius,
            maxRadius,
            border,
            config.MaskNoiseLarge,
            config.MaskNoiseMedium,
            config.MaskNoiseFine);

        float elongation = Math.Max(1f, config.MainIslandElongation);
        float cosRot = MathF.Cos(config.MainIslandRotation);
        float sinRot = MathF.Sin(config.MainIslandRotation);

        var satelliteCenters = new List<(float X, float Y, float Radius)>();
        int satelliteCount = Math.Clamp(config.SatelliteIslandCount, 0, 12);

        for (int i = 0; i < satelliteCount; i++)
        {
            float angle = random.NextFloat() * MathF.PI * 2f;
            float orbit = outerRadius * (0.62f + random.NextFloat() * 0.22f);
            float sx = mapCenterX + MathF.Cos(angle) * maxRadius * orbit;
            float sy = mapCenterY + MathF.Sin(angle) * maxRadius * orbit;
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
                float dx = (sampleX - islandCenterX) / maxRadius;
                float dy = (sampleY - islandCenterY) / maxRadius;

                float rotX = dx * cosRot - dy * sinRot;
                float rotY = dx * sinRot + dy * cosRot;
                float ellipticalDist = MathF.Sqrt(rotX * rotX + (rotY * rotY) / (elongation * elongation));

                float angle = MathF.Atan2(rotY, rotX);
                float radiusWarp = NoiseUtility.Fbm(
                    stageSeed + 20,
                    MathF.Cos(angle) * 4f + 0.5f,
                    MathF.Sin(angle) * 4f + 0.5f,
                    octaves: 3);
                float effectiveDist = ellipticalDist * (1f + (radiusWarp - 0.5f) * 0.28f);

                float largeNoise = NoiseUtility.Fbm(stageSeed + 10, wx * 2.2f, wy * 2.2f, octaves: 4);
                float mediumNoise = NoiseUtility.Fbm(stageSeed + 11, wx * 5.5f, wy * 5.5f, octaves: 3);
                float fineNoise = NoiseUtility.Fbm(stageSeed + 12, wx * 16f, wy * 16f, octaves: 2);
                float macroCoastNoise = NoiseUtility.Fbm(stageSeed + 21, wx * 3.5f, wy * 3.5f, octaves: 3);

                float sdf = safeOuterRadius - effectiveDist;
                sdf += (largeNoise - 0.5f) * config.MaskNoiseLarge;
                sdf += (mediumNoise - 0.5f) * config.MaskNoiseMedium;
                sdf += (fineNoise - 0.5f) * config.MaskNoiseFine;
                sdf += (macroCoastNoise - 0.5f) * config.MaskNoiseLarge * 0.35f;

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

                float edgeFalloff = IslandBorderUtility.ComputeEdgeFalloff(
                    x, y, width, height, border, stageSeed, wx, wy);
                mask *= edgeFalloff;

                plan.IslandMask[y * width + x] = Math.Clamp(mask, 0f, 1.25f);
            }
        }
    }
}
