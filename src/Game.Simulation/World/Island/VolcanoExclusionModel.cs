namespace Game.Simulation.World.Island;

public sealed class VolcanoExclusionModel
{
    public List<VolcanoExclusionZone> Zones { get; } = [];

    public bool IsProtected(int x, int y)
        => Zones.Any(zone => zone.Contains(x, y));

    public bool IsProtected(int x, int y, float additionalNormalizedRadius)
        => Zones.Any(zone => zone.Contains(x, y, additionalNormalizedRadius));

    public float DistanceToNearestBoundary(int x, int y)
    {
        float nearest = float.MaxValue;
        foreach (VolcanoExclusionZone zone in Zones)
        {
            nearest = MathF.Min(nearest, zone.NormalizedDistance(x, y) - zone.ProtectedRadius);
        }

        return nearest;
    }
}

public sealed class VolcanoExclusionZone
{
    public required int CenterX { get; init; }
    public required int CenterY { get; init; }
    public required float RadiusX { get; init; }
    public required float RadiusY { get; init; }
    public required float RotationRadians { get; init; }
    public required float ProtectedRadius { get; init; }

    public bool Contains(int x, int y, float additionalNormalizedRadius = 0f)
        => NormalizedDistance(x, y) <= ProtectedRadius + additionalNormalizedRadius;

    public float NormalizedDistance(int x, int y)
    {
        float dx = x - CenterX;
        float dy = y - CenterY;
        float cos = MathF.Cos(RotationRadians);
        float sin = MathF.Sin(RotationRadians);
        float localX = dx * cos + dy * sin;
        float localY = -dx * sin + dy * cos;
        float radiusX = MathF.Max(1f, RadiusX);
        float radiusY = MathF.Max(1f, RadiusY);
        return MathF.Sqrt((localX / radiusX) * (localX / radiusX) + (localY / radiusY) * (localY / radiusY));
    }
}
