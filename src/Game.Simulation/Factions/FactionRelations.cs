namespace Game.Simulation.Factions;

public enum FactionId
{
    Neutral = 0,
    Player = 1,
    Wildlife = 2
}

public static class FactionRelations
{
    public static bool IsHostile(FactionId a, FactionId b)
    {
        if (a == b)
        {
            return false;
        }

        return (a, b) switch
        {
            (FactionId.Wildlife, FactionId.Player) => true,
            (FactionId.Player, FactionId.Wildlife) => true,
            _ => false
        };
    }
}
