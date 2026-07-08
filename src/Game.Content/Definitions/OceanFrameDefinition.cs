namespace Game.Content.Definitions;

public sealed class OceanFrameDefinition
{
    public float OverscanScale { get; set; } = 1.25f;
    public int MinLandDistanceFromEdge { get; set; } = 40;
    public int MinCoastDistanceFromEdge { get; set; } = 24;
    public int MaxRegenerationAttempts { get; set; } = 8;
    public int MaxAxisAlignedCoastRun { get; set; } = 20;
    public int EdgeLinearityBand { get; set; } = 24;
}
