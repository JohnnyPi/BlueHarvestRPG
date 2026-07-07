namespace Game.Simulation.Time;

public sealed class ActorTurnState
{
    public int Energy { get; set; } = ActionCostTable.StartingEnergy;

    public int Speed { get; set; } = 100;

    public int EnergyRemainder { get; set; }

    public bool CanAct => Energy >= ActionCostTable.ActionThreshold;

    public void AddSpeedRecovery(int granularity)
    {
        int total = EnergyRemainder + Speed;
        EnergyRemainder = total % granularity;
        Energy = Math.Min(ActionCostTable.MaxEnergy, Energy + total / granularity);
    }

    public bool TrySpend(int cost)
    {
        if (Energy < cost)
        {
            return false;
        }

        Energy -= cost;
        return true;
    }
}
