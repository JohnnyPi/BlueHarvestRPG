namespace Game.Simulation.World.Island;

public sealed record TunnelSegment(
    int FromNodeId,
    int ToNodeId,
    IReadOnlyList<(int X, int Y)> Path);
