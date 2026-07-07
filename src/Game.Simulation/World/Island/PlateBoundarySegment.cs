namespace Game.Simulation.World.Island;

public sealed class PlateBoundarySegment
{
    public required int PlateAId { get; init; }
    public required int PlateBId { get; init; }
    public required PlateBoundaryType Type { get; init; }
    public required int CellX { get; init; }
    public required int CellY { get; init; }
}
