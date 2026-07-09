using Game.Simulation.Session;

namespace Game.Simulation.Character;

public static class ExperienceService
{
    public static int ExperienceForLevel(int level)
    {
        return 100 * level;
    }

    public static int TotalExperienceForLevel(int level)
    {
        int total = 0;
        for (int i = 1; i < level; i++)
        {
            total += ExperienceForLevel(i);
        }

        return total;
    }

    public static void GrantExperience(GameSession session, int amount, string reason)
    {
        if (amount <= 0)
        {
            return;
        }

        CharacterProgress progress = session.CharacterProgress;
        progress.Experience += amount;
        session.MessageLog.Add($"+{amount} XP ({reason}).");

        while (progress.Experience >= TotalExperienceForLevel(progress.Level + 1))
        {
            progress.Level++;
            ApplyLevelUp(session, progress);
        }

        session.MarkRenderDirty();
    }

    private static void ApplyLevelUp(GameSession session, CharacterProgress progress)
    {
        IncrementAttribute(progress, "strength");
        IncrementAttribute(progress, "vitality");
        session.ApplyAttributeBonuses();
        session.MessageLog.Add($"Level {progress.Level}! Strength and vitality increased.");
    }

    private static void IncrementAttribute(CharacterProgress progress, string id)
    {
        progress.Attributes.TryGetValue(id, out int current);
        progress.Attributes[id] = current + 1;
    }
}
