namespace Game.Simulation.World.Island;

public struct IslandCellData
{
    public bool IsLand;
    public bool IsCoast;
    public float Elevation;
    public float Moisture;
    public float Temperature;
    public float TectonicUplift;
    public float VolcanicActivity;
    public BiomeId Biome;
    public IslandCellRole Role;
    public PlateBoundaryType BoundaryType;
    public int RegionId;
}
