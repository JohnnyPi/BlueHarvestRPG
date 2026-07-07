namespace Game.Simulation.World;

[Flags]
public enum ConnectionFlags : ushort
{
    None = 0,
    NorthRoad = 1 << 0,
    EastRoad = 1 << 1,
    SouthRoad = 1 << 2,
    WestRoad = 1 << 3,
    NorthRiver = 1 << 4,
    EastRiver = 1 << 5,
    SouthRiver = 1 << 6,
    WestRiver = 1 << 7
}
