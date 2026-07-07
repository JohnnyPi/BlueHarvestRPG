namespace Game.Client.Debugging;

public sealed class DebugFrameStats
{
    public double UpdateMs { get; set; }
    public double DrawMs { get; set; }
    public int VisibleCells { get; set; }
    public int DrawRects { get; set; }
    public int SnapshotRebuildsThisSecond { get; set; }
    public bool SnapshotRebuiltThisFrame { get; set; }

    private int _frameCount;
    private double _fpsAccumulator;
    private double _fpsTimer;
    private int _rebuildCount;
    private double _rebuildTimer;

    public double Fps { get; private set; }
    public long GcGen0 { get; private set; }
    public long GcGen1 { get; private set; }
    public long GcGen2 { get; private set; }
    public long AllocatedBytes { get; private set; }

    public void BeginFrame()
    {
        GcGen0 = GC.CollectionCount(0);
        GcGen1 = GC.CollectionCount(1);
        GcGen2 = GC.CollectionCount(2);
        AllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
    }

    public void EndFrame(double elapsedSeconds)
    {
        _frameCount++;
        _fpsAccumulator += elapsedSeconds;
        _fpsTimer += elapsedSeconds;

        if (SnapshotRebuiltThisFrame)
        {
            _rebuildCount++;
        }

        _rebuildTimer += elapsedSeconds;

        if (_fpsTimer >= 1.0)
        {
            Fps = _frameCount / _fpsAccumulator;
            _frameCount = 0;
            _fpsAccumulator = 0;
            _fpsTimer = 0;
        }

        if (_rebuildTimer >= 1.0)
        {
            SnapshotRebuildsThisSecond = _rebuildCount;
            _rebuildCount = 0;
            _rebuildTimer = 0;
        }

        SnapshotRebuiltThisFrame = false;
    }

    public void NotifySnapshotRebuild()
    {
        SnapshotRebuiltThisFrame = true;
    }
}
