namespace Game.Simulation.World;

using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

public enum TileTransitionKind
{
    None,
    EnterStructure,
    ExitStructure,
    StairsUp,
    StairsDown,
    RopeDescent
}

public readonly record struct TileTransition(
    TileTransitionKind Kind,
    MapKey Destination,
    LocalCoord DestinationLocal,
    bool RequiresRope);
