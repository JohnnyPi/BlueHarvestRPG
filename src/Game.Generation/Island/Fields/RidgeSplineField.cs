using Game.Content.Definitions;

namespace Game.Generation.Island.Fields;

public static class RidgeSplineField
{
    public static float Sample(float px, float py, IReadOnlyList<IslandRidgeDefinition> ridges)
    {
        float height = 0f;

        foreach (IslandRidgeDefinition ridge in ridges)
        {
            if (ridge.Points.Length < 2)
            {
                continue;
            }

            float minDistSq = float.MaxValue;
            for (int segment = 0; segment < ridge.Points.Length - 1; segment++)
            {
                float ax = ridge.Points[segment].Length > 0 ? ridge.Points[segment][0] : 0f;
                float ay = ridge.Points[segment].Length > 1 ? ridge.Points[segment][1] : 0f;
                float bx = ridge.Points[segment + 1].Length > 0 ? ridge.Points[segment + 1][0] : 0f;
                float by = ridge.Points[segment + 1].Length > 1 ? ridge.Points[segment + 1][1] : 0f;

                float distSq = DistanceToSegmentSq(px, py, ax, ay, bx, by);
                minDistSq = MathF.Min(minDistSq, distSq);
            }

            float width = Math.Max(0.001f, ridge.Width);
            float ridgeHeight = MathF.Exp(-minDistSq / (width * width)) * ridge.Strength;
            height = MathF.Max(height, ridgeHeight);
        }

        return height;
    }

    public static float SampleAtCell(int x, int y, int width, int height, IReadOnlyList<IslandRidgeDefinition> ridges)
    {
        float px = x / (float)Math.Max(1, width - 1) * 2f - 1f;
        float py = y / (float)Math.Max(1, height - 1) * 2f - 1f;
        return Sample(px, py, ridges);
    }

    private static float DistanceToSegmentSq(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax;
        float aby = by - ay;
        float apx = px - ax;
        float apy = py - ay;
        float abLenSq = abx * abx + aby * aby;

        if (abLenSq <= 1e-6f)
        {
            float dx = px - ax;
            float dy = py - ay;
            return dx * dx + dy * dy;
        }

        float t = Math.Clamp((apx * abx + apy * aby) / abLenSq, 0f, 1f);
        float closestX = ax + abx * t;
        float closestY = ay + aby * t;
        float dx2 = px - closestX;
        float dy2 = py - closestY;
        return dx2 * dx2 + dy2 * dy2;
    }
}
