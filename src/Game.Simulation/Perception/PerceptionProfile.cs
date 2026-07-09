using Game.Simulation.Entities;
using Game.Simulation.Visibility;

namespace Game.Simulation.Perception;

public sealed class PerceptionProfile
{
    public int SightRadius { get; init; } = FovCalculator.DefaultCreatureRadius;
    public int HearingRange { get; init; } = 14;
    public int ScentRange { get; init; } = 3;
    public int ScentThreshold { get; init; } = 2;
    public int NoiseThreshold { get; init; } = 1;
    public int LoseSightTurns { get; init; } = 3;
    public int InvestigateTurns { get; init; } = 5;

    public static PerceptionProfile ForKind(EntityKind kind)
    {
        return kind switch
        {
            EntityKind.Raptor => new PerceptionProfile
            {
                SightRadius = 10,
                HearingRange = 14,
                ScentRange = 3,
                ScentThreshold = 2,
                NoiseThreshold = 1,
                LoseSightTurns = 3,
                InvestigateTurns = 5
            },
            EntityKind.Dilophosaur => new PerceptionProfile
            {
                SightRadius = 8,
                HearingRange = 18,
                ScentRange = 4,
                ScentThreshold = 2,
                NoiseThreshold = 1,
                LoseSightTurns = 2,
                InvestigateTurns = 4
            },
            EntityKind.Herbivore => new PerceptionProfile
            {
                SightRadius = 8,
                HearingRange = 12,
                ScentRange = 2,
                ScentThreshold = 3,
                NoiseThreshold = 2,
                LoseSightTurns = 2,
                InvestigateTurns = 3
            },
            _ => new PerceptionProfile()
        };
    }
}
