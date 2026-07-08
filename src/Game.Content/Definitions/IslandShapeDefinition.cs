namespace Game.Content.Definitions;

public sealed class IslandShapeDefinition
{
    public List<IslandBlobDefinition> AdditiveBlobs { get; set; } = [];
    public List<IslandBlobDefinition> SubtractiveBays { get; set; } = [];
    public IslandDomainWarpDefinition DomainWarp { get; set; } = new();
    public IslandCoastlineDetailDefinition CoastlineDetail { get; set; } = new();
    public float UnionSmoothness { get; set; } = 0.18f;
    public float SubtractSmoothness { get; set; } = 0.12f;
    public float LandThreshold { get; set; } = 0.02f;
}
