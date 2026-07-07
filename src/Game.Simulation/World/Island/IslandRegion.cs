namespace Game.Simulation.World.Island;

public sealed class IslandRegion
{
    public required int Id { get; init; }
    public required int SiteX { get; init; }
    public required int SiteY { get; init; }
    public BiomeId Theme { get; set; } = BiomeId.Plains;
    public bool IsContinental { get; set; } = true;
    public float MotionAngle { get; set; }
    public float MotionMagnitude { get; set; }
    public bool IsSatelliteIsland { get; set; }
    public bool IsMainIsland { get; set; }

    public (float X, float Y) MotionVector =>
        (MathF.Cos(MotionAngle) * MotionMagnitude, MathF.Sin(MotionAngle) * MotionMagnitude);
}
