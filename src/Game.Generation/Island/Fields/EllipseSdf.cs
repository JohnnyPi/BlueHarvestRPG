using Game.Content.Definitions;
using Game.Generation.Noise;

namespace Game.Generation.Island.Fields;

public static class EllipseSdf
{
    public static float Evaluate(float px, float py, IslandBlobDefinition blob, ulong warpSeed = 0)
    {
        float cx = blob.Center.Length > 0 ? blob.Center[0] : 0f;
        float cy = blob.Center.Length > 1 ? blob.Center[1] : 0f;
        float rx = blob.Radius.Length > 0 ? Math.Max(0.001f, blob.Radius[0]) : 0.5f;
        float ry = blob.Radius.Length > 1 ? Math.Max(0.001f, blob.Radius[1]) : rx;

        float dx = px - cx;
        float dy = py - cy;

        float radians = blob.RotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        float rotX = dx * cos - dy * sin;
        float rotY = dx * sin + dy * cos;

        if (warpSeed != 0)
        {
            float angle = MathF.Atan2(rotY, rotX);
            ulong blobSeed = warpSeed + (ulong)blob.Name.GetHashCode(StringComparison.Ordinal);
            float angularWarp = NoiseUtility.Fbm(
                blobSeed,
                MathF.Cos(angle) * 3f,
                MathF.Sin(angle) * 3f,
                octaves: 2);
            float radiusScale = 1f + (angularWarp - 0.5f) * 0.30f;
            rx *= radiusScale;
            ry *= radiusScale;
        }

        float nx = rotX / rx;
        float ny = rotY / ry;
        float dist = MathF.Sqrt(nx * nx + ny * ny);

        return (1f - dist) * blob.Strength;
    }
}
