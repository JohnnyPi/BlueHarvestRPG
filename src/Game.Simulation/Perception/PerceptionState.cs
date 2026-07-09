using Game.Simulation.Coordinates;

namespace Game.Simulation.Perception;

public sealed class PerceptionState
{
    public AwarenessLevel Awareness { get; set; } = AwarenessLevel.Unaware;

    public LocalCoord? LastKnownPosition { get; set; }

    public long LastSensedTurn { get; set; }

    public int InvestigateTurnsRemaining { get; set; }

    public int TurnsWithoutSight { get; set; }

    public AwarenessLevel PreviousAwareness { get; set; } = AwarenessLevel.Unaware;
}
