using Game.Content.Definitions;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class VolcanicConeUtility
{
    public const float LavaCoreRadiusFraction = 0.30f;
    public const float MountainRingRadiusFraction = 0.58f;
    public const float HillRingRadiusFraction = 0.88f;
    public const float ApronExtent = 1.25f;
    public const float ApronHeightFraction = 0.08f;

    public static float ComputeBaseRadiusCells(IslandPlan plan, IslandDefinition config)
    {
        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        return Math.Max(3f, maxRadius * config.VolcanicConeRadius);
    }

    public static float ComputeNormalizedDistance(VolcanicSite site, int x, int y)
    {
        float dx = x - site.X;
        float dy = y - site.Y;
        float cos = MathF.Cos(site.RotationRadians);
        float sin = MathF.Sin(site.RotationRadians);
        float localX = dx * cos + dy * sin;
        float localY = -dx * sin + dy * cos;
        float radiusX = MathF.Max(1f, site.RadiusX);
        float radiusY = MathF.Max(1f, site.RadiusY);
        return MathF.Sqrt((localX / radiusX) * (localX / radiusX) + (localY / radiusY) * (localY / radiusY));
    }

    public static bool IsInsideCone(VolcanicSite site, int x, int y)
    {
        return ComputeNormalizedDistance(site, x, y) <= 1f;
    }

    public static bool IsInsideLavaCore(VolcanicSite site, int x, int y)
    {
        return ComputeNormalizedDistance(site, x, y) <= LavaCoreRadiusFraction;
    }

    public static bool TryGetNearestConeDistance(IslandPlan plan, int x, int y, out float normalizedDistance)
    {
        normalizedDistance = float.MaxValue;
        foreach (VolcanicSite site in plan.VolcanicSites)
        {
            float distance = ComputeNormalizedDistance(site, x, y);
            normalizedDistance = MathF.Min(normalizedDistance, distance);
        }

        return normalizedDistance <= 1f;
    }

    public static float EvaluateElevationProfile(
        float footElevation,
        float coneHeight,
        float normalizedDistance)
    {
        float norm = MathF.Max(0f, normalizedDistance);
        float apronHeight = coneHeight * ApronHeightFraction;
        if (norm <= 1f)
        {
            return footElevation
                + apronHeight
                + (coneHeight - apronHeight) * SmoothFalloff(norm);
        }

        if (norm >= ApronExtent)
        {
            return footElevation;
        }

        float apronNorm = (norm - 1f) / (ApronExtent - 1f);
        return footElevation + apronHeight * SmoothFalloff(apronNorm);
    }

    private static float SmoothFalloff(float norm)
    {
        float t = Math.Clamp(1f - norm, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
