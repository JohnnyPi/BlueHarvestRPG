namespace Game.Simulation.World.Island;

public sealed class VolcanicSite
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required VolcanicOrigin Origin { get; init; }
    public required float Intensity { get; init; }
    public float RadiusX { get; init; } = 6f;
    public float RadiusY { get; init; } = 10f;
    public float RotationRadians { get; init; }
}
