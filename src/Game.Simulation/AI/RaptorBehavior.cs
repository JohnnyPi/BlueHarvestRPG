using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Ecology;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;

namespace Game.Simulation.AI;

public static class RaptorBehavior
{
    private static readonly (int dx, int dy)[] Directions =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
        (-1, -1),
        (1, -1),
        (-1, 1),
        (1, 1),
    ];

    private const int MinStalkDistance = 5;
    private const int MaxStalkDistance = 8;
    private const int RetreatAmbushDelay = 6;

    public static bool TryAct(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        if (entity.Kind is not (EntityKind.Raptor or EntityKind.Dilophosaur) || !entity.IsActive)
        {
            return false;
        }

        if (TryTriggerSnare(entity, session, map))
        {
            return true;
        }

        PerceptionSystem.Update(entity, session, map, worldTime);
        entity.Perception ??= new PerceptionState();
        PerceptionState perception = entity.Perception;
        DriveResolver.Resolve(entity, session, map);

        entity.Raptor ??= new RaptorMemory();
        RaptorMemory memory = entity.Raptor;

        if (memory.Phase == RaptorPhase.ProbeFence)
        {
            return ActProbeFence(entity, session, map, memory);
        }

        if (perception.Awareness == AwarenessLevel.Unaware)
        {
            if (IsAdjacentToFence(map, entity.LocalPosition))
            {
                memory.Phase = RaptorPhase.ProbeFence;
                return ActProbeFence(entity, session, map, memory);
            }

            return WanderGoal.TryWander(entity, map, worldTime);
        }

        if (perception.Awareness == AwarenessLevel.Suspicious)
        {
            return ActInvestigate(entity, map, perception);
        }

        if (memory.Phase == RaptorPhase.ProbeFence)
        {
            return ActProbeFence(entity, session, map, memory);
        }

        LocalCoord target = ResolveTarget(entity, session, map, perception);
        int distance = Manhattan(entity.LocalPosition, target);

        if (memory.Phase == RaptorPhase.Retreat &&
            perception.Awareness >= AwarenessLevel.Suspicious)
        {
            return ActRetreat(entity, session, map, target, memory, distance);
        }

        if (memory.Phase == RaptorPhase.Ambush &&
            perception.Awareness >= AwarenessLevel.Tracking)
        {
            return ActAmbush(entity, session, map, target, distance);
        }

        if (perception.Awareness == AwarenessLevel.Tracking)
        {
            return ActTracking(entity, session, map, memory, target, distance);
        }

        if (perception.Awareness != AwarenessLevel.Engaged)
        {
            return false;
        }

        return memory.Phase switch
        {
            RaptorPhase.Ambush => ActAmbush(entity, session, map, target, distance),
            RaptorPhase.Retreat => ActRetreat(entity, session, map, target, memory, distance),
            _ => ActStalk(entity, session, map, target, memory, distance)
        };
    }

    public static void OnDamaged(GameSession session, Entity raptor)
    {
        if (raptor.Kind is not (EntityKind.Raptor or EntityKind.Dilophosaur))
        {
            return;
        }

        raptor.Raptor ??= new RaptorMemory();
        raptor.Raptor.Phase = RaptorPhase.Retreat;
        raptor.Raptor.AmbushCooldown = RetreatAmbushDelay;
        session.FinaleThreats.Record(FinaleThreatId.RaptorPack);
        session.MessageLog.Add("The raptor falls back into cover.");
        session.MarkRenderDirty();
    }

    public static string Describe(Entity entity)
    {
        if (entity.Kind is not (EntityKind.Raptor or EntityKind.Dilophosaur) || entity.Raptor is null)
        {
            return entity.Kind == EntityKind.Dilophosaur ? "Dilophosaur" : "Raptor";
        }

        return entity.Raptor.Phase switch
        {
            RaptorPhase.Stalk => $"{entity.Kind} (stalking)",
            RaptorPhase.ProbeFence => $"{entity.Kind} (testing fence)",
            RaptorPhase.Retreat => $"{entity.Kind} (retreating)",
            RaptorPhase.Ambush => $"{entity.Kind} (ambushing)",
            _ => entity.Kind.ToString()
        };
    }

    private static LocalCoord ResolveTarget(Entity entity, GameSession session, LocalMap map, PerceptionState perception)
    {
        if (entity.Drive?.ActiveDrive == CreatureDrive.Hunger &&
            entity.Drive.HuntTargetId is ulong preyId)
        {
            Entity? prey = map.Entities.GetById(new EntityId(preyId));
            if (prey is not null && prey.IsActive)
            {
                return prey.LocalPosition;
            }
        }

        if (perception.Awareness == AwarenessLevel.Engaged)
        {
            return session.PlayerLocalPosition;
        }

        return perception.LastKnownPosition ?? entity.LocalPosition;
    }

    private static bool ActInvestigate(Entity entity, LocalMap map, PerceptionState perception)
    {
        if (perception.LastKnownPosition is null)
        {
            return false;
        }

        return TryStep(entity, map, StepToward(entity.LocalPosition, perception.LastKnownPosition.Value, map, entity));
    }

    private static bool ActTracking(
        Entity entity,
        GameSession session,
        LocalMap map,
        RaptorMemory memory,
        LocalCoord target,
        int distance)
    {
        if (distance <= 1 && entity.Drive?.ActiveDrive == CreatureDrive.Hunger)
        {
            Entity? prey = map.Entities.GetAt(target);
            if (prey is not null && prey.Kind == EntityKind.Herbivore)
            {
                var combat = new CombatResolver();
                return combat.TryAttack(session, entity, prey);
            }
        }

        if (distance > MaxStalkDistance)
        {
            return TryStep(entity, map, StepToward(entity.LocalPosition, target, map, entity));
        }

        return TryStep(entity, map, PickFlankMove(entity.LocalPosition, target, map, entity, preferCloser: true));
    }

    private static bool ActStalk(
        Entity entity,
        GameSession session,
        LocalMap map,
        LocalCoord target,
        RaptorMemory memory,
        int distance)
    {
        if (IsAdjacentToFence(map, entity.LocalPosition))
        {
            memory.Phase = RaptorPhase.ProbeFence;
            return ActProbeFence(entity, session, map, memory);
        }

        if (distance <= 3)
        {
            return TryStep(entity, map, PickMoveAwayFrom(entity.LocalPosition, target, map, entity));
        }

        if (distance > MaxStalkDistance)
        {
            return TryStep(entity, map, PickFlankMove(entity.LocalPosition, target, map, entity, preferCloser: true));
        }

        return TryStep(entity, map, PickFlankMove(entity.LocalPosition, target, map, entity, preferCloser: false));
    }

    private static bool ActProbeFence(Entity entity, GameSession session, LocalMap map, RaptorMemory memory)
    {
        if (!memory.AnnouncedFenceProbe)
        {
            memory.AnnouncedFenceProbe = true;
            session.MessageLog.Add("A raptor tests the perimeter fence.");
            session.MarkRenderDirty();
        }

        LocalCoord? gate = FindNearestDoor(map, entity.LocalPosition);
        if (gate is not null)
        {
            if (TryStep(entity, map, StepToward(entity.LocalPosition, gate.Value, map, entity)))
            {
                return true;
            }
        }

        LocalCoord? alongFence = PickFencePaceMove(map, entity);
        if (TryStep(entity, map, alongFence))
        {
            return true;
        }

        memory.Phase = RaptorPhase.Stalk;
        return false;
    }

    private static bool ActRetreat(
        Entity entity,
        GameSession session,
        LocalMap map,
        LocalCoord target,
        RaptorMemory memory,
        int distance)
    {
        memory.AmbushCooldown = Math.Max(0, memory.AmbushCooldown - 1);

        if (memory.AmbushCooldown == 0 && distance >= MinStalkDistance)
        {
            memory.Phase = RaptorPhase.Ambush;
            session.MessageLog.Add("The raptor circles back for another strike.");
            session.MarkRenderDirty();
            return ActAmbush(entity, session, map, target, distance);
        }

        if (distance <= 4)
        {
            return TryStep(entity, map, PickMoveAwayFrom(entity.LocalPosition, target, map, entity));
        }

        return TryStep(entity, map, PickFlankMove(entity.LocalPosition, target, map, entity, preferCloser: false));
    }

    private static bool ActAmbush(
        Entity entity,
        GameSession session,
        LocalMap map,
        LocalCoord target,
        int distance)
    {
        if (distance <= 1)
        {
            Entity? defender = map.Entities.GetAt(target);
            if (defender is not null && defender.IsActive && FactionRelations.IsHostile(entity.Faction, defender.Faction))
            {
                var combat = new CombatResolver();
                if (combat.TryAttack(session, entity, defender))
                {
                    memoryResetAfterStrike(entity);
                    return true;
                }
            }
        }

        if (TryStep(entity, map, StepToward(entity.LocalPosition, target, map, entity)))
        {
            return true;
        }

        entity.Raptor!.Phase = RaptorPhase.Stalk;
        return false;
    }

    private static bool TryTriggerSnare(Entity entity, GameSession session, LocalMap map)
    {
        Entity? trap = map.Entities.GetAt(entity.LocalPosition);
        if (trap is null || trap.Kind != EntityKind.SnareTrap || !trap.IsActive)
        {
            return false;
        }

        entity.Health = Math.Max(0, entity.Health - 6);
        entity.ImmobilizedTurns = 2;
        trap.IsActive = false;
        map.Entities.Remove(trap.Id);
        NoiseEmitter.EmitCombat(session, entity.LocalPosition);
        session.MessageLog.Add("A snare snaps shut on the raptor!");
        session.MarkRenderDirty();
        return true;
    }

    private static void memoryResetAfterStrike(Entity entity)
    {
        if (entity.Raptor is null)
        {
            return;
        }

        entity.Raptor.Phase = RaptorPhase.Retreat;
        entity.Raptor.AmbushCooldown = RetreatAmbushDelay - 2;
    }

    private static bool TryStep(Entity entity, LocalMap map, LocalCoord? next)
    {
        if (next is null)
        {
            return false;
        }

        LocalCoord from = entity.LocalPosition;
        EntityFacing.UpdateFromMove(entity, from, next.Value);
        entity.LocalPosition = next.Value;
        return true;
    }

    private static LocalCoord? PickMoveAwayFrom(
        LocalCoord from,
        LocalCoord target,
        LocalMap map,
        Entity self)
    {
        LocalCoord? best = null;
        int bestDistance = -1;

        foreach ((int dx, int dy) in Directions)
        {
            var candidate = new LocalCoord(from.X + dx, from.Y + dy);
            if (!CanEnter(map, self, candidate))
            {
                continue;
            }

            int distance = Manhattan(candidate, target);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static LocalCoord? PickFlankMove(
        LocalCoord from,
        LocalCoord player,
        LocalMap map,
        Entity self,
        bool preferCloser)
    {
        LocalCoord? best = null;
        int bestScore = int.MinValue;

        foreach ((int dx, int dy) in Directions)
        {
            var candidate = new LocalCoord(from.X + dx, from.Y + dy);
            if (!CanEnter(map, self, candidate))
            {
                continue;
            }

            int distance = Manhattan(candidate, player);
            int flankScore = -Math.Abs(distance - 6) * 10;
            flankScore -= Math.Abs(dx) == Math.Abs(dy) ? 0 : 2;
            flankScore += preferCloser ? -distance : Math.Min(0, distance - MinStalkDistance);

            if (flankScore > bestScore)
            {
                bestScore = flankScore;
                best = candidate;
            }
        }

        return best;
    }

    private static LocalCoord? StepToward(
        LocalCoord from,
        LocalCoord target,
        LocalMap map,
        Entity self)
    {
        LocalCoord? best = null;
        int bestDistance = int.MaxValue;

        foreach ((int dx, int dy) in Directions)
        {
            var candidate = new LocalCoord(from.X + dx, from.Y + dy);
            if (!CanEnter(map, self, candidate))
            {
                continue;
            }

            int distance = Manhattan(candidate, target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static LocalCoord? PickFencePaceMove(LocalMap map, Entity self)
    {
        LocalCoord from = self.LocalPosition;
        LocalCoord? best = null;
        int bestFenceNeighbors = -1;

        foreach ((int dx, int dy) in Directions)
        {
            var candidate = new LocalCoord(from.X + dx, from.Y + dy);
            if (!CanEnter(map, self, candidate))
            {
                continue;
            }

            int fenceNeighbors = CountAdjacentTerrain(map, candidate, TerrainId.Fence);
            if (fenceNeighbors > bestFenceNeighbors)
            {
                bestFenceNeighbors = fenceNeighbors;
                best = candidate;
            }
        }

        return best;
    }

    private static bool IsAdjacentToFence(LocalMap map, LocalCoord position)
    {
        foreach ((int dx, int dy) in Directions)
        {
            var neighbor = new LocalCoord(position.X + dx, position.Y + dy);
            if (!map.Contains(neighbor))
            {
                continue;
            }

            if (map.Terrain[map.GetIndex(neighbor.X, neighbor.Y)] == TerrainId.Fence)
            {
                return true;
            }
        }

        return false;
    }

    private static LocalCoord? FindNearestDoor(LocalMap map, LocalCoord from)
    {
        LocalCoord? best = null;
        int bestDistance = int.MaxValue;

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                if (map.Terrain[map.GetIndex(x, y)] != TerrainId.Door)
                {
                    continue;
                }

                int distance = Manhattan(from, new LocalCoord(x, y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = new LocalCoord(x, y);
                }
            }
        }

        return best;
    }

    private static int CountAdjacentTerrain(LocalMap map, LocalCoord position, TerrainId terrain)
    {
        int count = 0;
        foreach ((int dx, int dy) in Directions)
        {
            var neighbor = new LocalCoord(position.X + dx, position.Y + dy);
            if (!map.Contains(neighbor))
            {
                continue;
            }

            if (map.Terrain[map.GetIndex(neighbor.X, neighbor.Y)] == terrain)
            {
                count++;
            }
        }

        return count;
    }

    private static bool CanEnter(LocalMap map, Entity self, LocalCoord position)
    {
        if (!map.Contains(position) || map.BlocksMovement(position))
        {
            return false;
        }

        Entity? occupant = map.Entities.GetAt(position);
        return occupant is null || occupant.Id == self.Id || !occupant.BlocksMovement;
    }

    private static int Manhattan(LocalCoord a, LocalCoord b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
