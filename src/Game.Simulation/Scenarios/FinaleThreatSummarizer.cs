using Game.Simulation.Session;

namespace Game.Simulation.Scenarios;

public static class FinaleThreatSummarizer
{
    public static string BuildFinaleText(GameSession session, EscapeEndingKind ending)
    {
        List<string> beats = [];

        if (session.FinaleThreats.Contains(FinaleThreatId.RaptorPack))
        {
            beats.Add("the raptor pack shadows the evacuation route");
        }

        if (session.FinaleThreats.Contains(FinaleThreatId.PowerFailure))
        {
            beats.Add("dead power sectors force a dangerous detour");
        }

        if (session.FinaleThreats.Contains(FinaleThreatId.StormFront))
        {
            beats.Add("the storm turns every road into a gamble");
        }

        if (session.FinaleThreats.Contains(FinaleThreatId.MissedEvacuation))
        {
            beats.Add("the missed evacuation window leaves you bargaining for any ride off-island");
        }

        if (ending == EscapeEndingKind.Survival && !session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery))
        {
            beats.Add("the unanswered mystery follows you home");
        }

        if (beats.Count == 0)
        {
            return string.Empty;
        }

        return " Finale complications: " + string.Join("; ", beats) + ".";
    }
}
