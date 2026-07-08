using Game.Content.Definitions;

namespace Game.Generation.Island.Fields;

public static class EllipseSdf
{
    public static float Evaluate(float px, float py, IslandBlobDefinition blob)
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

        float nx = rotX / rx;
        float ny = rotY / ry;
        float dist = MathF.Sqrt(nx * nx + ny * ny);

        return (1f - dist) * blob.Strength;
    }
}
