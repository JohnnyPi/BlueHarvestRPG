namespace Game.Simulation.World;

public struct WorldCell
{
    public float Elevation;
    public float Moisture;
    public float Temperature;
    public BiomeId Biome;
    public bool HasLocalChanges;
    public ConnectionFlags ConnectionFlags;
}
