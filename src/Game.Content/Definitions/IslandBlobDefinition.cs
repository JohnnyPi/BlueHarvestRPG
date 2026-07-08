namespace Game.Content.Definitions;

public sealed class IslandBlobDefinition
{
    public string Name { get; set; } = string.Empty;
    public float[] Center { get; set; } = [0f, 0f];
    public float[] Radius { get; set; } = [0.5f, 0.5f];
    public float RotationDegrees { get; set; }
    public float Strength { get; set; } = 1f;
    public float Smoothness { get; set; } = 0.18f;
}
