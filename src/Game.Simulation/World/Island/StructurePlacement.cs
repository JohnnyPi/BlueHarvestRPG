namespace Game.Simulation.World.Island;

public sealed record StructurePlacement(
    int InstanceId,
    StructureType Type,
    string BlueprintId,
    int FloorCount,
    int BasementCount,
    int GlobalOriginX,
    int GlobalOriginY,
    int Width,
    int Height)
{
    public int MinFloorIndex => -BasementCount;

    public int MaxFloorIndex => FloorCount - 1;

    public bool HasFloor(int floorIndex) => floorIndex >= MinFloorIndex && floorIndex <= MaxFloorIndex;

    public static StructurePlacement CreatePending(
        StructureType type,
        int globalOriginX,
        int globalOriginY,
        int width,
        int height) =>
        new(0, type, string.Empty, 1, 0, globalOriginX, globalOriginY, width, height);
}
