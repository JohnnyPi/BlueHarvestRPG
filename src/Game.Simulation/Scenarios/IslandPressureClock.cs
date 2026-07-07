namespace Game.Simulation.Scenarios;

public sealed class IslandPressureClock
{
    public const int MaxPressure = 100;

    public int Pressure { get; private set; }

    public int LastEventThreshold { get; private set; }

    public void Restore(int pressure, int lastEventThreshold)
    {
        Pressure = Math.Clamp(pressure, 0, MaxPressure);
        LastEventThreshold = Math.Max(0, lastEventThreshold);
    }

    public void Advance(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Pressure = Math.Min(MaxPressure, Pressure + Math.Max(1, amount / 50));
    }

    public bool TryConsumeEvent(out int threshold, out string message)
    {
        threshold = Pressure / 20;
        if (threshold <= LastEventThreshold)
        {
            message = string.Empty;
            return false;
        }

        LastEventThreshold = threshold;
        message = threshold switch
        {
            1 => "A distant roar echoes closer through the canopy.",
            2 => "Storm clouds thicken along the western coast. Travel grows harder.",
            3 => "Another sector of the power grid fails. A route is blocked.",
            4 => $"Evacuation opens for {PressureEventResolver.EvacWindowHours} hours. Reach your escape route.",
            _ => "Final evacuation call! The window is closing fast."
        };
        return true;
    }
}
