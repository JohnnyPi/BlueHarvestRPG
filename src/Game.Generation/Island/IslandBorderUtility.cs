using Game.Generation.Noise;

namespace Game.Generation.Island;

internal static class IslandBorderUtility
{
    private const float RadiusWarpMinFactor = 0.86f;

    public static float ComputeSafeOuterRadius(
        float outerRadius,
        float maxRadius,
        int border,
        float maskNoiseLarge,
        float maskNoiseMedium,
        float maskNoiseFine)
    {
        if (border <= 0)
        {
            return outerRadius;
        }

        float borderNorm = border / maxRadius;
        float noiseBudget = maskNoiseLarge * 0.5f + maskNoiseMedium * 0.5f + maskNoiseFine * 0.5f;
        float warpBudget = (1f - RadiusWarpMinFactor) / RadiusWarpMinFactor;
        float maxSafe = (1f - borderNorm - noiseBudget - 0.04f) / (1f + warpBudget);
        return Math.Min(outerRadius, Math.Max(0.2f, maxSafe));
    }

    public static float ComputeEdgeFalloff(
        int x,
        int y,
        int width,
        int height,
        int border,
        ulong seed,
        float wx,
        float wy)
    {
        if (border <= 0)
        {
            return 1f;
        }

        float edgeDist = Math.Min(
            Math.Min(x, y),
            Math.Min(width - 1 - x, height - 1 - y));

        if (edgeDist < border)
        {
            return 0f;
        }

        float rampEnd = border * 2f;
        if (edgeDist >= rampEnd)
        {
            return 1f;
        }

        float falloff = NoiseUtility.SmoothStep(border, rampEnd, edgeDist);
        float edgeNoise = NoiseUtility.Fbm(seed + 14, wx * 8f, wy * 8f, octaves: 2);
        falloff *= 0.78f + edgeNoise * 0.44f;

        return Math.Clamp(falloff, 0f, 1f);
    }

    public static void ClampElevationInBorderBand(
        ref float elevation,
        int x,
        int y,
        int width,
        int height,
        int border,
        float landThreshold)
    {
        if (border <= 0)
        {
            return;
        }

        float edgeDist = Math.Min(
            Math.Min(x, y),
            Math.Min(width - 1 - x, height - 1 - y));

        if (edgeDist < border)
        {
            elevation = MathF.Min(elevation, landThreshold * 0.5f);
        }
    }
}
