using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Seeds;

namespace Game.Simulation.AI;

public static class WanderGoal
{
    private static readonly (int dx, int dy)[] Directions =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
    ];

    public static bool TryWander(Entity entity, LocalMap map, long worldTime)
    {
        ulong seed = SeedUtility.Derive(
            (ulong)entity.Id.Value,
            entity.LocalPosition.X,
            entity.LocalPosition.Y,
            (uint)(worldTime & 0xFFFF_FFFF));

        int startDirection = (int)(seed % (ulong)Directions.Length);

        for (int offset = 0; offset < Directions.Length; offset++)
        {
            (int dx, int dy) = Directions[(startDirection + offset) % Directions.Length];
            var next = new LocalCoord(entity.LocalPosition.X + dx, entity.LocalPosition.Y + dy);

            if (!map.Contains(next) || map.BlocksMovement(next))
            {
                continue;
            }

            if (IsOccupiedByOtherCreature(map, entity, next))
            {
                continue;
            }

            LocalCoord from = entity.LocalPosition;
            EntityFacing.UpdateFromMove(entity, from, next);
            entity.LocalPosition = next;
            return true;
        }

        return false;
    }

    private static bool IsOccupiedByOtherCreature(LocalMap map, Entity self, LocalCoord position)
    {
        Entity? occupant = map.Entities.GetAt(position);
        return occupant is not null &&
               occupant.Id != self.Id &&
               occupant.BlocksMovement;
    }
}
