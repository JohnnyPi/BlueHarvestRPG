namespace Game.Simulation.World.Island;

public enum TunnelNodeKind : byte
{
    Junction,
    PaddockAccess,
    MaintenanceAccess,
    VisitorHub,
    Cavern
}

public sealed record TunnelNode(
    int Id,
    int GlobalX,
    int GlobalY,
    TunnelNodeKind Kind);
