namespace Game.Simulation.World.Island;

[Flags]
public enum IslandCellRole : ushort
{
    None = 0,
    Coast = 1 << 0,
    Dock = 1 << 1,
    Helipad = 1 << 2,
    Hotel = 1 << 3,
    Restaurant = 1 << 4,
    Attraction = 1 << 5,
    VisitorCenter = 1 << 6,
    Paddock = 1 << 7,
    Maintenance = 1 << 8,
    Tunnel = 1 << 9,
    Cavern = 1 << 10,
    Ruin = 1 << 11,
    Fortification = 1 << 12,
    Road = 1 << 13
}
