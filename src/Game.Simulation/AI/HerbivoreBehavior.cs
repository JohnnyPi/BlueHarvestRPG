using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Session;

namespace Game.Simulation.AI;

public static class HerbivoreBehavior
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

    public static bool TryAct(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        if (entity.Kind != EntityKind.Herbivore || !entity.IsActive)
        {
            return false;
        }

        PerceptionSystem.Update(entity, session, map, worldTime);
        entity.Perception ??= new PerceptionState();
        Ecology.DriveResolver.Resolve(entity, session, map);

        if (entity.Drive?.ActiveDrive == Ecology.CreatureDrive.Flee)
        {
            LocalCoord? threat = FindNearestThreat(map, entity);
            if (threat is not null)
            {
                return TryStep(entity, map, StepAwayFrom(entity.LocalPosition, threat.Value, map, entity));
            }
        }

        if (entity.Perception.Awareness == AwarenessLevel.Unaware)
        {
            return WanderGoal.TryWander(entity, map, worldTime);
        }

        return TryStep(entity, map, StepAwayFrom(entity.LocalPosition, session.PlayerLocalPosition, map, entity));
    }

    private static LocalCoord? FindNearestThreat(LocalMap map, Entity self)
    {
        Entity? nearest = null;
        int bestDistance = int.MaxValue;

        foreach (Entity entity in map.Entities.All)
        {
            if (!entity.IsActive || entity.Id == self.Id)
            {
                continue;
            }

            if (entity.Kind is not (EntityKind.Raptor or EntityKind.Dilophosaur or EntityKind.Player))
            {
                continue;
            }

            int distance = Manhattan(entity.LocalPosition, self.LocalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = entity;
            }
        }

        return nearest?.LocalPosition;
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

    private static LocalCoord? StepAwayFrom(LocalCoord from, LocalCoord target, LocalMap map, Entity self)
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
