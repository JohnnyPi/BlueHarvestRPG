using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Time;

public sealed class TurnScheduler
{
    private const int MaxRecoverySteps = 10_000;
    private const int EnergyGranularity = 10;

    public SimulationClock Clock { get; } = new();

    public void AdvanceOverworldTravelStep(GameSession session)
    {
        for (int i = 0; i < BiomeTraversal.OverworldTravelHours; i++)
        {
            Clock.Advance();
            session.NotifyWorldHourElapsed();
        }

        session.PlayerTurnState.AddSpeedRecovery(EnergyGranularity);
    }

    public void RunOverworldRest(GameSession session)
    {
        ActorTurnState player = session.PlayerTurnState;

        int safety = 0;
        while (player.Energy < ActionCostTable.MaxEnergy && safety++ < MaxRecoverySteps)
        {
            Clock.Advance();
            session.NotifyWorldHourElapsed();
            player.AddSpeedRecovery(EnergyGranularity);
        }
    }

    public void RunUntilPlayerReady(
        GameSession session,
        Func<Entity, LocalMap, bool> tryActorAct)
    {
        if (session.ViewMode != GameViewMode.LocalMap || session.ActiveLocalMap is null)
        {
            return;
        }

        LocalMap map = session.ActiveLocalMap;
        foreach (Entity entity in map.Entities.All)
        {
            if (!entity.IsActive || entity.Actor is null || entity.Id == EntityId.Player)
            {
                continue;
            }

            entity.Actor.AddSpeedRecovery(EnergyGranularity);
        }

        RunActorActions(session, map, tryActorAct);
    }

    private static void RunActorActions(
        GameSession session,
        LocalMap map,
        Func<Entity, LocalMap, bool> tryActorAct)
    {
        const int maxActionsPerStep = 64;
        int actions = 0;

        while (actions++ < maxActionsPerStep)
        {
            Entity? next = null;
            int highestEnergy = ActionCostTable.ActionThreshold - 1;
            ulong lowestId = ulong.MaxValue;

            foreach (Entity entity in map.Entities.All)
            {
                if (!entity.IsActive || entity.Actor is null || entity.Id == EntityId.Player)
                {
                    continue;
                }

                if (entity.Actor.Energy >= ActionCostTable.ActionThreshold)
                {
                    if (entity.Actor.Energy > highestEnergy ||
                        (entity.Actor.Energy == highestEnergy && entity.Id.Value < lowestId))
                    {
                        highestEnergy = entity.Actor.Energy;
                        lowestId = entity.Id.Value;
                        next = entity;
                    }
                }
            }

            if (next is null)
            {
                break;
            }

            ActorTurnState actor = next.Actor!;
            if (!actor.TrySpend(ActionCostTable.Walk))
            {
                break;
            }

            tryActorAct(next, map);
            session.MarkRenderDirty();
        }
    }
}
