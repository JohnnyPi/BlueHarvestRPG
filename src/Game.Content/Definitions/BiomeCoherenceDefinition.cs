namespace Game.Content.Definitions;

public sealed class BiomeCoherenceDefinition
{
    public bool Enabled { get; set; } = true;
    public bool UseEightWayNeighbors { get; set; } = true;
    public int MinPatchPlains { get; set; } = 16;
    public int MinPatchForest { get; set; } = 20;
    public int MinPatchJungle { get; set; } = 28;
    public int MinPatchSwamp { get; set; } = 18;
    public int MinPatchHills { get; set; } = 14;
    public int MinPatchMountains { get; set; } = 10;
    public int MinPatchBeach { get; set; } = 2;
    public int MinPatchVolcanic { get; set; } = 4;
}
