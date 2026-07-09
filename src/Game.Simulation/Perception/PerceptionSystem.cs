using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.Visibility;

namespace Game.Simulation.Perception;

public static class PerceptionSystem
{
    public static void Update(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        if (!entity.IsActive || !entity.IsAlive)
        {
            return;
        }

        if (entity.Kind is not (EntityKind.Raptor or EntityKind.Dilophosaur or EntityKind.Herbivore or EntityKind.WanderingCreature))
        {
            return;
        }

        entity.Perception ??= new PerceptionState();
        PerceptionState state = entity.Perception;
        PerceptionProfile profile = PerceptionProfile.ForKind(entity.Kind);

        LocalCoord playerPosition = session.PlayerLocalPosition;
        bool canSeePlayer = CanSeeTarget(map, entity.LocalPosition, playerPosition, profile.SightRadius);
        NoiseDetection noiseResult = DetectNoise(map, entity.LocalPosition, profile);
        byte scentStrength = map.Scent.GetMaxInRange(map, entity.LocalPosition, profile.ScentRange);

        AwarenessLevel previous = state.Awareness;
        UpdateAwareness(
            state,
            profile,
            worldTime,
            playerPosition,
            map,
            entity.LocalPosition,
            canSeePlayer,
            noiseResult,
            scentStrength);

        if (state.Awareness != previous && entity.Kind == EntityKind.Raptor)
        {
            EmitPlayerFeedback(session, state.Awareness, previous);
        }

        state.PreviousAwareness = state.Awareness;
    }

    public static void InitializeAtSpawn(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        entity.Perception ??= new PerceptionState();
        PerceptionProfile profile = PerceptionProfile.ForKind(entity.Kind);
        LocalCoord playerPosition = session.PlayerLocalPosition;
        bool canSeePlayer = CanSeeTarget(map, entity.LocalPosition, playerPosition, profile.SightRadius);

        if (canSeePlayer)
        {
            entity.Perception.Awareness = AwarenessLevel.Engaged;
            entity.Perception.LastKnownPosition = playerPosition;
            entity.Perception.LastSensedTurn = worldTime;
            entity.Perception.TurnsWithoutSight = 0;
            return;
        }

        NoiseDetection noiseResult = DetectNoise(map, entity.LocalPosition, profile);
        byte scentStrength = map.Scent.GetMaxInRange(map, entity.LocalPosition, profile.ScentRange);

        if (noiseResult.Strength >= profile.NoiseThreshold)
        {
            entity.Perception.Awareness = AwarenessLevel.Suspicious;
            entity.Perception.LastKnownPosition = noiseResult.Source;
            entity.Perception.InvestigateTurnsRemaining = profile.InvestigateTurns;
        }
        else if (scentStrength >= profile.ScentThreshold)
        {
            entity.Perception.Awareness = AwarenessLevel.Suspicious;
            entity.Perception.LastKnownPosition = FindStrongestScentTile(map, entity.LocalPosition, profile.ScentRange);
            entity.Perception.InvestigateTurnsRemaining = profile.InvestigateTurns;
        }
        else
        {
            entity.Perception.Awareness = AwarenessLevel.Unaware;
        }

        entity.Perception.LastSensedTurn = worldTime;
    }

    public static bool CanSeeTarget(LocalMap map, LocalCoord from, LocalCoord to, int sightRadius)
    {
        int effectiveRadius = sightRadius;
        if (map.ReducesVision(to) || map.ReducesVision(from))
        {
            effectiveRadius = Math.Max(1, sightRadius - 1);
        }

        return FovCalculator.CanSee(map, from, to, effectiveRadius);
    }

    public static LocalCoord? GetTargetPosition(Entity entity, GameSession session, PerceptionState state)
    {
        if (state.Awareness == AwarenessLevel.Engaged)
        {
            return session.PlayerLocalPosition;
        }

        return state.LastKnownPosition;
    }

    public static bool KnowsPlayerLocation(PerceptionState state)
    {
        return state.Awareness is AwarenessLevel.Tracking or AwarenessLevel.Engaged;
    }

    public static void DecayFields(LocalMap map, long worldTime)
    {
        map.Noise.Decay(worldTime);
        map.Scent.Decay();
    }

    public static void DepositScent(LocalMap map, LocalCoord position, byte amount = 2)
    {
        map.Scent.Deposit(map, position, amount);
    }

    private static void UpdateAwareness(
        PerceptionState state,
        PerceptionProfile profile,
        long worldTime,
        LocalCoord playerPosition,
        LocalMap map,
        LocalCoord observerPosition,
        bool canSeePlayer,
        NoiseDetection noiseResult,
        byte scentStrength)
    {
        if (canSeePlayer)
        {
            state.Awareness = AwarenessLevel.Engaged;
            state.LastKnownPosition = playerPosition;
            state.LastSensedTurn = worldTime;
            state.TurnsWithoutSight = 0;
            state.InvestigateTurnsRemaining = profile.InvestigateTurns;
            return;
        }

        if (state.Awareness == AwarenessLevel.Engaged)
        {
            state.TurnsWithoutSight++;
            if (state.TurnsWithoutSight >= profile.LoseSightTurns)
            {
                state.Awareness = AwarenessLevel.Tracking;
                state.TurnsWithoutSight = 0;
            }

            return;
        }

        bool heardNoise = noiseResult.Strength >= profile.NoiseThreshold;
        bool smelledScent = scentStrength >= profile.ScentThreshold;

        if (heardNoise || smelledScent)
        {
            LocalCoord signalSource = heardNoise
                ? noiseResult.Source
                : FindStrongestScentTile(map, observerPosition, profile.ScentRange);

            state.LastKnownPosition = signalSource;
            state.LastSensedTurn = worldTime;

            if (state.Awareness == AwarenessLevel.Unaware)
            {
                state.Awareness = AwarenessLevel.Suspicious;
                state.InvestigateTurnsRemaining = profile.InvestigateTurns;
            }
            else if (state.Awareness == AwarenessLevel.Suspicious &&
                     (heardNoise && noiseResult.Strength >= profile.NoiseThreshold + 1 || smelledScent))
            {
                state.Awareness = AwarenessLevel.Tracking;
                state.InvestigateTurnsRemaining = profile.InvestigateTurns;
            }
            else if (state.Awareness == AwarenessLevel.Tracking)
            {
                state.InvestigateTurnsRemaining = profile.InvestigateTurns;
            }

            return;
        }

        if (state.Awareness == AwarenessLevel.Tracking)
        {
            state.Awareness = AwarenessLevel.Suspicious;
            state.InvestigateTurnsRemaining = Math.Max(1, state.InvestigateTurnsRemaining);
            return;
        }

        if (state.Awareness == AwarenessLevel.Suspicious)
        {
            state.InvestigateTurnsRemaining--;
            if (state.InvestigateTurnsRemaining <= 0)
            {
                state.Awareness = AwarenessLevel.Unaware;
                state.LastKnownPosition = null;
            }
        }
    }

    private static LocalCoord FindStrongestScentTile(LocalMap map, LocalCoord origin, int range)
    {
        byte max = 0;
        LocalCoord best = origin;

        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                var coord = new LocalCoord(origin.X + dx, origin.Y + dy);
                if (!map.Contains(coord))
                {
                    continue;
                }

                byte intensity = map.Scent.GetIntensity(map, coord);
                if (intensity > max)
                {
                    max = intensity;
                    best = coord;
                }
            }
        }

        return best;
    }

    private static NoiseDetection DetectNoise(LocalMap map, LocalCoord origin, PerceptionProfile profile)
    {
        byte bestStrength = 0;
        LocalCoord bestSource = origin;

        for (int dy = -profile.HearingRange; dy <= profile.HearingRange; dy++)
        {
            for (int dx = -profile.HearingRange; dx <= profile.HearingRange; dx++)
            {
                var coord = new LocalCoord(origin.X + dx, origin.Y + dy);
                if (!map.Contains(coord))
                {
                    continue;
                }

                int manhattan = Math.Abs(dx) + Math.Abs(dy);
                if (manhattan > profile.HearingRange)
                {
                    continue;
                }

                byte raw = map.Noise.GetStrength(map, coord);
                if (raw == 0)
                {
                    continue;
                }

                int attenuated = raw - manhattan / 2;
                if (!FovCalculator.HasLineOfSight(map, origin, coord) && manhattan > 1)
                {
                    attenuated /= 2;
                }

                if (attenuated > bestStrength)
                {
                    bestStrength = (byte)Math.Clamp(attenuated, 0, 255);
                    bestSource = coord;
                }
            }
        }

        return new NoiseDetection(bestStrength, bestSource);
    }

    private static void EmitPlayerFeedback(GameSession session, AwarenessLevel current, AwarenessLevel previous)
    {
        if (current > previous)
        {
            string message = current switch
            {
                AwarenessLevel.Suspicious => "A raptor pauses, listening…",
                AwarenessLevel.Tracking => "Something is moving through the brush nearby.",
                AwarenessLevel.Engaged => "A raptor has spotted you!",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(message))
            {
                session.MessageLog.Add(message);
                session.MarkRenderDirty();
            }
        }
        else if (current < previous)
        {
            string message = current switch
            {
                AwarenessLevel.Tracking => "The raptor loses sight of you.",
                AwarenessLevel.Suspicious => "The raptor seems uncertain.",
                AwarenessLevel.Unaware => "You are out of the raptor's notice.",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(message))
            {
                session.MessageLog.Add(message);
                session.MarkRenderDirty();
            }
        }
    }

    private readonly record struct NoiseDetection(byte Strength, LocalCoord Source);
}
