using Game.Simulation.Coordinates;

namespace Game.Simulation.World;

public readonly record struct MapTransition(
    WorldCoord DestinationWorld,
    LocalCoord DestinationLocal);
