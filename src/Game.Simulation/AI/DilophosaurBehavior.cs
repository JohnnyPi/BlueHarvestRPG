using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;

namespace Game.Simulation.AI;

public static class DilophosaurBehavior
{
    public static bool TryAct(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        if (entity.Kind != EntityKind.Dilophosaur || !entity.IsActive)
        {
            return false;
        }

        return RaptorBehavior.TryAct(entity, session, map, worldTime);
    }
}
