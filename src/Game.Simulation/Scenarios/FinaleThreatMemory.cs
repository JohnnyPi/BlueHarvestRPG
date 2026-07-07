namespace Game.Simulation.Scenarios;

public enum FinaleThreatId
{
    RaptorPack = 1,
    StormFront = 2,
    PowerFailure = 3,
    MissedEvacuation = 4
}

public sealed class FinaleThreatMemory
{
    private readonly HashSet<FinaleThreatId> _threats = [];

    public IReadOnlyCollection<FinaleThreatId> Threats => _threats;

    public void Record(FinaleThreatId threat)
    {
        _threats.Add(threat);
    }

    public bool Contains(FinaleThreatId threat) => _threats.Contains(threat);

    public void Restore(IEnumerable<FinaleThreatId> threats)
    {
        _threats.Clear();
        foreach (FinaleThreatId threat in threats)
        {
            _threats.Add(threat);
        }
    }
}
