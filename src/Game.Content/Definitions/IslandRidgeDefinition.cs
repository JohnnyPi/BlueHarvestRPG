namespace Game.Content.Definitions;

public sealed class IslandRidgeDefinition
{
    public string Name { get; set; } = string.Empty;
    public float[][] Points { get; set; } = [];
    public float Strength { get; set; } = 0.22f;
    public float Width { get; set; } = 0.12f;
}
