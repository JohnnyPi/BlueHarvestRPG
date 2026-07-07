using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;

namespace Game.Simulation.AI;

public static class CreatureAi
{
    public static bool TryAct(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        return entity.Kind switch
        {
            EntityKind.Raptor => RaptorBehavior.TryAct(entity, session, map, worldTime),
            EntityKind.WanderingCreature => WanderGoal.TryWander(entity, map, worldTime),
            _ => false
        };
    }
}
