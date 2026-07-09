using Game.Simulation.Coordinates;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.World.Island;

namespace Game.Simulation.Scenarios;

public static class ScenarioObjectiveTracker
{
    public static void Check(GameSession session)
    {
        RunScenario? scenario = session.RunScenario;
        if (scenario is null)
        {
            return;
        }

        CheckEscape(session, scenario);
        CheckMystery(session, scenario);
        CheckEndure(session);
    }

    private static void CheckEscape(GameSession session, RunScenario scenario)
    {
        if (session.QuestLog.IsCompleted(ScenarioQuestIds.Escape))
        {
            return;
        }

        if (scenario.EscapeTarget is not WorldCoord target)
        {
            return;
        }

        if (session.ViewMode != GameViewMode.Overworld ||
            session.PlayerWorldPosition != target)
        {
            return;
        }

        session.QuestLog.Advance(ScenarioQuestIds.Escape, 1, 1, session);
        session.MessageLog.Add($"Reached escape route: {scenario.EscapeRoute} ({scenario.EscapeLandmark}).");
        session.MarkRenderDirty();
        EscapeVictoryResolver.TryCompleteEscape(session);
    }

    private static void CheckMystery(GameSession session, RunScenario scenario)
    {
        if (session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery))
        {
            return;
        }

        if (scenario.MysteryTarget is not WorldCoord target)
        {
            return;
        }

        if (session.ViewMode != GameViewMode.LocalMap ||
            session.PlayerWorldPosition != target)
        {
            return;
        }

        session.QuestLog.Advance(ScenarioQuestIds.Mystery, 1, 1, session);
        session.MessageLog.Add($"Investigated mystery site: {scenario.MysteryLandmark}.");
        session.MarkRenderDirty();
    }

    private static void CheckEndure(GameSession session)
    {
        if (session.QuestLog.IsCompleted(ScenarioQuestIds.Endure))
        {
            return;
        }

        int pressure = session.PressureClock.Pressure;
        session.QuestLog.SetProgress(
            ScenarioQuestIds.Endure,
            pressure,
            ScenarioQuestIds.EndurePressureTarget,
            session);

        if (session.QuestLog.IsCompleted(ScenarioQuestIds.Endure))
        {
            session.MessageLog.Add("Survived long enough for the island pressure to escalate.");
            session.MarkRenderDirty();
        }
    }
}
