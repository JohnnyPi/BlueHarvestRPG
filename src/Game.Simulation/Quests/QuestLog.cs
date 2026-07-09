using Game.Simulation.Character;
using Game.Simulation.Session;

namespace Game.Simulation.Quests;

public enum QuestState
{
    NotStarted,
    Active,
    Completed
}

public sealed class QuestProgressEntry
{
    public required string QuestId { get; init; }
    public QuestState State { get; set; }
    public int Progress { get; set; }
}

public sealed class QuestLog
{
    private readonly Dictionary<string, QuestProgressEntry> _entries = [];

    public IReadOnlyCollection<QuestProgressEntry> Progress => _entries.Values;

    public void Restore(string questId, QuestState state, int progress)
    {
        _entries[questId] = new QuestProgressEntry
        {
            QuestId = questId,
            State = state,
            Progress = progress
        };
    }

    public void Start(string questId)
    {
        if (!_entries.ContainsKey(questId))
        {
            _entries[questId] = new QuestProgressEntry
            {
                QuestId = questId,
                State = QuestState.Active,
                Progress = 0
            };
        }
    }

    public void Advance(string questId, int amount, int target, GameSession? session = null)
    {
        if (!_entries.TryGetValue(questId, out QuestProgressEntry? entry))
        {
            Start(questId);
            entry = _entries[questId];
        }

        bool wasCompleted = entry.State == QuestState.Completed;
        entry.Progress = Math.Min(target, entry.Progress + amount);
        if (entry.Progress >= target)
        {
            entry.State = QuestState.Completed;
        }

        if (session is not null && !wasCompleted && entry.State == QuestState.Completed)
        {
            int xp = questId switch
            {
                Scenarios.ScenarioQuestIds.Escape => 100,
                Scenarios.ScenarioQuestIds.Mystery => 75,
                Scenarios.ScenarioQuestIds.Endure => 50,
                "first_kill" => 25,
                "gather_wood" => 15,
                _ => 20
            };
            ExperienceService.GrantExperience(session, xp, $"quest: {questId}");
        }
    }

    public void SetProgress(string questId, int progress, int target, GameSession? session = null)
    {
        if (!_entries.TryGetValue(questId, out QuestProgressEntry? entry))
        {
            Start(questId);
            entry = _entries[questId];
        }

        bool wasCompleted = entry.State == QuestState.Completed;
        entry.Progress = Math.Min(target, Math.Max(0, progress));
        if (entry.Progress >= target)
        {
            entry.State = QuestState.Completed;
        }

        if (session is not null && !wasCompleted && entry.State == QuestState.Completed)
        {
            ExperienceService.GrantExperience(session, 50, $"quest: {questId}");
        }
    }

    public bool IsCompleted(string questId)
    {
        return _entries.TryGetValue(questId, out QuestProgressEntry? entry) &&
               entry.State == QuestState.Completed;
    }
}
