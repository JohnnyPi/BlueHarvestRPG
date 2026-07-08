namespace Game.Generation.Island;

public sealed record IslandGenerationStageTiming(string Name, double DurationMs);

public sealed record IslandGenerationProgressSnapshot(
    string? CurrentStage,
    double CurrentStageElapsedMs,
    double TotalElapsedMs,
    IReadOnlyList<IslandGenerationStageTiming> CompletedStages)
{
    public string BriefStatus => CurrentStage is null
        ? "Starting..."
        : $"{CurrentStage} ({FormatSeconds(CurrentStageElapsedMs)})";

    public static string FormatSeconds(double milliseconds)
        => milliseconds >= 10_000
            ? $"{milliseconds / 1000:0}s"
            : $"{milliseconds / 1000:0.0}s";

    public static string FormatDuration(double milliseconds)
        => milliseconds >= 10_000
            ? $"{milliseconds / 1000:0}s"
            : milliseconds >= 1000
                ? $"{milliseconds / 1000:0.00}s"
                : $"{milliseconds:0}ms";
}

public sealed class IslandGenerationProgressReporter
{
    private readonly object _lock = new();
    private readonly List<IslandGenerationStageTiming> _completedStages = [];
    private readonly Stack<(string Name, DateTime StartUtc)> _stageStack = new();
    private string? _currentStage;
    private DateTime _generationStartUtc;
    private DateTime _stageStartUtc;
    private bool _started;

    public void RunStage(string name, Action action)
    {
        BeginStage(name);
        try
        {
            action();
        }
        finally
        {
            EndStage();
        }
    }

    public void BeginStage(string name)
    {
        lock (_lock)
        {
            if (!_started)
            {
                _started = true;
                _generationStartUtc = DateTime.UtcNow;
            }

            DateTime now = DateTime.UtcNow;
            _stageStack.Push((name, now));
            _currentStage = name;
            _stageStartUtc = now;
        }
    }

    public void EndStage()
    {
        lock (_lock)
        {
            if (_stageStack.Count == 0)
            {
                return;
            }

            (string name, DateTime startUtc) = _stageStack.Pop();
            double durationMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;
            _completedStages.Add(new IslandGenerationStageTiming(name, durationMs));

            if (_stageStack.Count > 0)
            {
                (string parentName, DateTime parentStartUtc) = _stageStack.Peek();
                _currentStage = parentName;
                _stageStartUtc = parentStartUtc;
            }
            else
            {
                _currentStage = null;
            }
        }
    }

    public IslandGenerationProgressSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            DateTime now = DateTime.UtcNow;
            double totalMs = _started ? (now - _generationStartUtc).TotalMilliseconds : 0;
            double currentMs = _currentStage is null ? 0 : (now - _stageStartUtc).TotalMilliseconds;
            return new IslandGenerationProgressSnapshot(
                _currentStage,
                currentMs,
                totalMs,
                _completedStages.ToArray());
        }
    }
}
