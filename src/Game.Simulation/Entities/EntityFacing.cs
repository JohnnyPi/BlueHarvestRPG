using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Simulation.Entities;

public static class EntityFacing
{
    public static void UpdateFromMove(Entity entity, LocalCoord from, LocalCoord to)
    {
        int deltaX = to.X - from.X;
        int deltaY = to.Y - from.Y;
        if (DirectionResolver.TryFromDelta(deltaX, deltaY, out Direction facing))
        {
            entity.Facing = facing;
        }
    }
}
