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

    public void Advance(string questId, int amount, int target)
    {
        if (!_entries.TryGetValue(questId, out QuestProgressEntry? entry))
        {
            Start(questId);
            entry = _entries[questId];
        }

        entry.Progress = Math.Min(target, entry.Progress + amount);
        if (entry.Progress >= target)
        {
            entry.State = QuestState.Completed;
        }
    }

    public void SetProgress(string questId, int progress, int target)
    {
        if (!_entries.TryGetValue(questId, out QuestProgressEntry? entry))
        {
            Start(questId);
            entry = _entries[questId];
        }

        entry.Progress = Math.Min(target, Math.Max(0, progress));
        if (entry.Progress >= target)
        {
            entry.State = QuestState.Completed;
        }
    }

    public bool IsCompleted(string questId)
    {
        return _entries.TryGetValue(questId, out QuestProgressEntry? entry) &&
               entry.State == QuestState.Completed;
    }
}
