using Game.Simulation.Entities;
using Game.Simulation.Session;
using Game.Simulation.Time;

namespace Game.Simulation.Scenarios;

public static class ScenarioEncounterResolver
{
    public static void TryTriggerFirstEncounter(GameSession session)
    {
        if (session.FirstEncounterTriggered || session.RunScenario is null)
        {
            return;
        }

        session.FirstEncounterTriggered = true;
        string encounter = session.RunScenario.FirstEncounter;
        session.MessageLog.Add($"First encounter: {encounter}.");

        if (encounter.Contains("raptor", StringComparison.OrdinalIgnoreCase) ||
            encounter.Contains("Dilophosaur", StringComparison.OrdinalIgnoreCase))
        {
            if (EntityFactory.TrySpawnEncounterPredator(session))
            {
                session.MessageLog.Add("Predator eyes reflect in the undergrowth.");
            }

            return;
        }

        if (encounter.Contains("triceratops", StringComparison.OrdinalIgnoreCase) ||
            encounter.Contains("hadrosaur", StringComparison.OrdinalIgnoreCase) ||
            encounter.Contains("Ankylosaur", StringComparison.OrdinalIgnoreCase))
        {
            session.MessageLog.Add("The herd is too close to risk a direct approach.");
            session.AdvancePressureClock(ActionCostTable.Wait);
        }
    }
}
