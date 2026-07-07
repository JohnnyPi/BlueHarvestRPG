namespace Game.Simulation.World.Island;

public sealed record StructurePlacement(
    StructureType Type,
    int GlobalOriginX,
    int GlobalOriginY,
    int Width,
    int Height);
