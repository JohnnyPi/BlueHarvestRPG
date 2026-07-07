namespace Game.Simulation.Time;

public sealed class SimulationClock
{
    public long WorldTime { get; private set; }

    public int Day { get; private set; }

    public int Hour { get; private set; }

    public void Advance()
    {
        WorldTime++;
        Hour = (int)(WorldTime % 24);
        Day = (int)(WorldTime / 24);
    }

    public void Reset()
    {
        WorldTime = 0;
        Day = 0;
        Hour = 0;
    }

    public void Restore(long worldTime)
    {
        WorldTime = Math.Max(0, worldTime);
        Hour = (int)(WorldTime % 24);
        Day = (int)(WorldTime / 24);
    }
}
