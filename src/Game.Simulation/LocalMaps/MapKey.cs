using Game.Simulation.Coordinates;

namespace Game.Simulation.LocalMaps;

public readonly record struct MapKey(WorldCoord WorldPosition, int StructureInstanceId, int FloorIndex)
{
    public bool IsSurface => StructureInstanceId == 0;

    public bool IsStructureInterior => StructureInstanceId > 0;

    public static MapKey Surface(WorldCoord worldPosition) => new(worldPosition, 0, 0);
}
