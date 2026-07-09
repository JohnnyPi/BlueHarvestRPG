namespace Game.Generation.Island.Fields;

public static class ShapeFieldComposer
{
    public static float SmoothUnion(float a, float b, float smoothness)
    {
        if (smoothness <= 0f)
        {
            return MathF.Max(a, b);
        }

        return SmoothMax(a, b, smoothness);
    }

    public static float SmoothSubtract(float baseField, float cutField, float smoothness)
    {
        if (cutField <= 0f)
        {
            return baseField;
        }

        return SmoothMax(baseField, -cutField, smoothness);
    }

    public static float EvaluateIslandSdf(
        float px,
        float py,
        IReadOnlyList<Game.Content.Definitions.IslandBlobDefinition> additiveBlobs,
        IReadOnlyList<Game.Content.Definitions.IslandBlobDefinition> subtractiveBays,
        float unionSmoothness,
        float subtractSmoothness,
        ulong shapeSeed = 0)
    {
        float island = float.NegativeInfinity;

        foreach (Game.Content.Definitions.IslandBlobDefinition blob in additiveBlobs)
        {
            float blobSdf = EllipseSdf.Evaluate(px, py, blob, shapeSeed);
            float smoothness = blob.Smoothness > 0f ? blob.Smoothness : unionSmoothness;
            island = island == float.NegativeInfinity
                ? blobSdf
                : SmoothUnion(island, blobSdf, smoothness);
        }

        if (island == float.NegativeInfinity)
        {
            island = -1f;
        }

        foreach (Game.Content.Definitions.IslandBlobDefinition bay in subtractiveBays)
        {
            float baySdf = EllipseSdf.Evaluate(px, py, bay, shapeSeed);
            float smoothness = bay.Smoothness > 0f ? bay.Smoothness : subtractSmoothness;
            island = SmoothSubtract(island, baySdf, smoothness);
        }

        return island;
    }

    private static float SmoothMax(float a, float b, float smoothness)
    {
        return -SmoothMin(-a, -b, smoothness);
    }

    private static float SmoothMin(float a, float b, float smoothness)
    {
        float h = Math.Clamp(0.5f + 0.5f * (b - a) / smoothness, 0f, 1f);
        return b + (a - b) * h - smoothness * h * (1f - h);
    }
}
