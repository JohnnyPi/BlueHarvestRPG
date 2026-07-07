using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class LocalGenerationContext
{
    public required ulong Seed { get; init; }
    public required WorldCoord WorldCoordinate { get; init; }
    public required WorldCell WorldCell { get; init; }
    public required IReadOnlyList<EdgeConnection> Connections { get; init; }
    public IslandPlan? IslandPlan { get; init; }
}
