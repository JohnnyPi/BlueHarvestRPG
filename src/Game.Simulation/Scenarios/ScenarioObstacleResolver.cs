using Game.Simulation.Coordinates;
using Game.Simulation.Quests;
using Game.Simulation.Session;

namespace Game.Simulation.Scenarios;

public static class ScenarioObstacleResolver
{
    public static bool IsBlocked(GameSession session, WorldCoord coord)
    {
        if (session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery))
        {
            return false;
        }

        RunScenario? scenario = session.RunScenario;
        if (scenario is null)
        {
            return false;
        }

        return coord == scenario.Obstacle1Target || coord == scenario.Obstacle2Target;
    }

    public static string DescribeBlockedTravel(GameSession session, WorldCoord target)
    {
        RunScenario? scenario = session.RunScenario;
        if (scenario is null)
        {
            return string.Empty;
        }

        if (target == scenario.Obstacle1Target)
        {
            return $"Blocked: {scenario.Obstacle1}. Investigate the mystery to find another way.";
        }

        if (target == scenario.Obstacle2Target)
        {
            return $"Blocked: {scenario.Obstacle2}. Investigate the mystery to find another way.";
        }

        return string.Empty;
    }
}
