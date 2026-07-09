namespace Game.Simulation.Ecology;

[Flags]
public enum CreatureDrive
{
    None = 0,
    Patrol = 1 << 0,
    Hunger = 1 << 1,
    Flee = 1 << 2,
    Herd = 1 << 3
}
