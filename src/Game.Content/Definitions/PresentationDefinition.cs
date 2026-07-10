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

public sealed class ElevationShadeProfileDefinition
{
    public bool Enabled { get; set; } = true;
    public float DarkestElevation { get; set; } = 0.35f;
    public float FullBrightnessElevation { get; set; } = 0.9f;
    public float MaxDarkening { get; set; } = 0.24f;
}

public sealed class ElevationShadingDefinition
{
    public bool Enabled { get; set; } = true;
    public ElevationShadeProfileDefinition DefaultProfile { get; set; } = new();
    public Dictionary<string, ElevationShadeProfileDefinition> Biomes { get; set; } = [];
    public List<string> ExcludedBiomes { get; set; } = ["Ocean", "ShallowWater", "Reef"];
}

public sealed class TilesDefinition
{
    public Dictionary<string, string> Terrain { get; set; } = [];
    public Dictionary<string, string> Biomes { get; set; } = [];
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

public sealed class PlayerCharacterDefinition
{
    public string Texture { get; set; } = string.Empty;
    public string? IdleTexture { get; set; }
}

public sealed class PlayerDefinition
{
    public string DefaultCharacter { get; set; } = "Char_001";
    public int FrameWidth { get; set; } = 32;
    public int FrameHeight { get; set; } = 32;
    public int Columns { get; set; } = 4;
    public int Rows { get; set; } = 4;
    public int WalkFrameDurationMs { get; set; } = 125;
    public int StepAnimationDurationMs { get; set; } = 250;
    public Dictionary<string, PlayerCharacterDefinition> Characters { get; set; } = [];
}

public sealed class CreatureSpriteDefinition
{
    public string Texture { get; set; } = string.Empty;
    public float TileWidth { get; set; } = 1f;
    public float TileHeight { get; set; } = 1f;
}

public sealed class CreaturesDefinition
{
    public Dictionary<string, string> KindBindings { get; set; } = [];
    public Dictionary<string, CreatureSpriteDefinition> Creatures { get; set; } = [];
}
