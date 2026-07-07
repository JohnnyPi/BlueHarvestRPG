namespace Game.Simulation.World.Island;

public sealed class VolcanicSite
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required VolcanicOrigin Origin { get; init; }
    public required float Intensity { get; init; }
}
