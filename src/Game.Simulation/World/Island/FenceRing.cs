namespace Game.Simulation.World.Island;

public sealed record FenceRing(
    int GlobalCenterX,
    int GlobalCenterY,
    int Radius,
    int GateGlobalX,
    int GateGlobalY,
    int PaddockIndex);
