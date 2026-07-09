namespace Game.Simulation.LocalMaps;

[Flags]
public enum TileFlags : byte
{
    None = 0,
    BlocksMovement = 1 << 0,
    BlocksVision = 1 << 1,
    ContainsWater = 1 << 2,
    ReducesVision = 1 << 3
}
