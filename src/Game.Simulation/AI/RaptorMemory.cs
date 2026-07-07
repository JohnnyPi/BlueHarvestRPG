namespace Game.Simulation.AI;

public enum RaptorPhase
{
    Stalk,
    ProbeFence,
    Retreat,
    Ambush
}

public sealed class RaptorMemory
{
    public RaptorPhase Phase { get; set; } = RaptorPhase.Stalk;

    public int AmbushCooldown { get; set; }

    public bool AnnouncedFenceProbe { get; set; }
}
