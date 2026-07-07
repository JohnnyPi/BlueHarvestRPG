namespace Game.Simulation.Entities;

public readonly record struct EntityId(ulong Value)
{
    public static EntityId Player => new(0);

    public bool IsPlayer => Value == 0;

    public static bool IsReserved(ulong value) => value == 0;
}
