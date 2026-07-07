namespace Game.Content.Definitions;

public sealed class ColorEntry
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#FF00FF";
}

public sealed class BiomeColorsDefinition
{
    public Dictionary<string, ColorEntry> Biomes { get; set; } = [];
}

public sealed class TerrainColorsDefinition
{
    public Dictionary<string, ColorEntry> Terrain { get; set; } = [];
}

public sealed class CameraDefinition
{
    public int BaseCellSize { get; set; } = 8;
    public float MinZoom { get; set; } = 0.25f;
    public float MaxZoom { get; set; } = 8f;
    public float ZoomStep { get; set; } = 1.15f;
    public float PanSpeed { get; set; } = 600f;
    public string SelectionColor { get; set; } = "#FFD700";
    public string PlayerColor { get; set; } = "#FFFF00";
}
