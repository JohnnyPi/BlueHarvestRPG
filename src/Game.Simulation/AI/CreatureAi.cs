using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Session;

namespace Game.Simulation.AI;

public static class CreatureAi
{
    public static bool TryAct(Entity entity, GameSession session, LocalMap map, long worldTime)
    {
        if (entity.ImmobilizedTurns > 0)
        {
            entity.ImmobilizedTurns--;
            return true;
        }

        entity.StatusEffects?.Tick(session, entity);

        return entity.Kind switch
        {
            EntityKind.Raptor => RaptorBehavior.TryAct(entity, session, map, worldTime),
            EntityKind.Dilophosaur => DilophosaurBehavior.TryAct(entity, session, map, worldTime),
            EntityKind.Herbivore => HerbivoreBehavior.TryAct(entity, session, map, worldTime),
            EntityKind.WanderingCreature => WanderGoal.TryWander(entity, map, worldTime),
            _ => false
        };
    }
}
