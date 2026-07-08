using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
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
        if (entity.Kind != EntityKind.Raptor || !entity.IsActive)
        {
            return false;
        }

        entity.Raptor ??= new RaptorMemory();
        RaptorMemory memory = entity.Raptor;
        LocalCoord player = session.PlayerLocalPosition;
        int distance = Manhattan(entity.LocalPosition, player);

        return memory.Phase switch
        {
            RaptorPhase.Ambush => ActAmbush(entity, session, map, player, distance),
            RaptorPhase.Retreat => ActRetreat(entity, session, map, player, memory, distance),
            RaptorPhase.ProbeFence => ActProbeFence(entity, session, map, memory),
            _ => ActStalk(entity, session, map, player, memory, distance)
        };
    }

    public static void OnDamaged(GameSession session, Entity raptor)
    {
        if (raptor.Kind != EntityKind.Raptor)
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
        if (entity.Kind != EntityKind.Raptor || entity.Raptor is null)
        {
            return "Raptor";
        }

        return entity.Raptor.Phase switch
        {
            RaptorPhase.Stalk => "Raptor (stalking)",
            RaptorPhase.ProbeFence => "Raptor (testing fence)",
            RaptorPhase.Retreat => "Raptor (retreating)",
            RaptorPhase.Ambush => "Raptor (ambushing)",
            _ => "Raptor"
        };
    }

    private static bool ActStalk(
        Entity entity,
        GameSession session,
        LocalMap map,
        LocalCoord player,
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
            return TryStep(entity, map, PickMoveAwayFrom(entity.LocalPosition, player, map, entity));
        }

        if (distance > MaxStalkDistance)
        {
            return TryStep(entity, map, PickFlankMove(entity.LocalPosition, player, map, entity, preferCloser: true));
        }

        return TryStep(entity, map, PickFlankMove(entity.LocalPosition, player, map, entity, preferCloser: false));
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
        LocalCoord player,
        RaptorMemory memory,
        int distance)
    {
        memory.AmbushCooldown = Math.Max(0, memory.AmbushCooldown - 1);

        if (memory.AmbushCooldown == 0 && distance >= MinStalkDistance)
        {
            memory.Phase = RaptorPhase.Ambush;
            session.MessageLog.Add("The raptor circles back for another strike.");
            session.MarkRenderDirty();
            return ActAmbush(entity, session, map, player, distance);
        }

        if (distance <= 4)
        {
            return TryStep(entity, map, PickMoveAwayFrom(entity.LocalPosition, player, map, entity));
        }

        return TryStep(entity, map, PickFlankMove(entity.LocalPosition, player, map, entity, preferCloser: false));
    }

    private static bool ActAmbush(
        Entity entity,
        GameSession session,
        LocalMap map,
        LocalCoord player,
        int distance)
    {
        if (distance <= 1)
        {
            Entity playerEntity = session.PlayerEntity;
            var combat = new CombatResolver();
            if (combat.TryAttack(session, entity, playerEntity))
            {
                memoryResetAfterStrike(entity);
                return true;
            }
        }

        if (TryStep(entity, map, StepToward(entity.LocalPosition, player, map, entity)))
        {
            return true;
        }

        entity.Raptor!.Phase = RaptorPhase.Stalk;
        return false;
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
