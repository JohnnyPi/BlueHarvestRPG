using Game.Simulation.Session;

namespace Game.Simulation.Scenarios;

public static class EscapeVictoryResolver
{
    public static void TryCompleteEscape(GameSession session)
    {
        if (session.IsRunComplete || !session.QuestLog.IsCompleted(ScenarioQuestIds.Escape))
        {
            return;
        }

        bool mysterySolved = session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery);
        EscapeEndingKind ending = mysterySolved ? EscapeEndingKind.Resolved : EscapeEndingKind.Survival;
        RunScenario? scenario = session.RunScenario;

        string title = ending == EscapeEndingKind.Resolved ? "Truth Escapes With You" : "Off the Island";
        string summary = BuildEscapeSummary(session, scenario, ending);

        session.CompleteRun(RunOutcome.Escaped, ending, title, summary);
        session.MessageLog.Add(ending == EscapeEndingKind.Resolved
            ? "You escape the island with answers in hand."
            : "You escape the island alive, but questions remain.");
    }

    public static void MarkPlayerDead(GameSession session)
    {
        if (session.IsRunComplete)
        {
            return;
        }

        RunScenario? scenario = session.RunScenario;
        string summary = scenario is null
            ? "The island claims another survivor."
            : $"You never reached {scenario.EscapeLandmark}. {scenario.Mystery} dies with you.";

        session.CompleteRun(RunOutcome.Dead, EscapeEndingKind.None, "Lost on the Island", summary);
        session.MessageLog.Add("You have been killed.");
    }

    private static string BuildEscapeSummary(GameSession session, RunScenario? scenario, EscapeEndingKind ending)
    {
        string finaleText = FinaleThreatSummarizer.BuildFinaleText(session, ending);
        if (scenario is null)
        {
            string fallback = ending == EscapeEndingKind.Resolved
                ? "You made it off the island with the truth."
                : "You survived the island, but the mystery endures.";

            return fallback + finaleText;
        }

        if (ending == EscapeEndingKind.Resolved)
        {
            return $"You reached {scenario.EscapeLandmark} via {scenario.EscapeRoute}. " +
                   $"The secret is out: {scenario.IslandSecret}" +
                   finaleText;
        }

        return $"You reached {scenario.EscapeLandmark} via {scenario.EscapeRoute}, " +
               $"but never learned why {scenario.Mystery}" +
               finaleText;
    }
}
