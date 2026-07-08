namespace Game.Content.Definitions;

public sealed class IslandCoastlineDetailDefinition
{
    public float Frequency { get; set; } = 5f;
    public float Amplitude { get; set; } = 0.025f;
    public bool PreserveLargeBays { get; set; } = true;
    public int CellularAutomataIterations { get; set; } = 3;
    public int ProceduralInletCount { get; set; } = 6;
}
