namespace Game.Generation.Island;

public sealed class GenerationDiagnosticsOptions
{
    public bool CaptureSnapshots { get; set; } = true;
    public bool RunQualityGate { get; set; }
    public IslandGenerationProgressReporter? Progress { get; set; }
}
