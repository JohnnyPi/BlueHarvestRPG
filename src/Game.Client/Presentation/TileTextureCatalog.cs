using Game.Client.Debugging;
using Game.Content.Definitions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Presentation;

internal sealed class TileTextureCatalog
{
    private const string TilesetRoot = "Textures/Tilesets";

    private readonly Texture2D?[] _terrainTextures;
    private readonly Texture2D?[] _biomeTextures;

    public bool HasTerrainTiles { get; private set; }
    public bool HasBiomeTiles { get; private set; }

    public TileTextureCatalog(ContentManager content, TilesDefinition tiles)
    {
        _terrainTextures = new Texture2D?[Enum.GetValues<TerrainId>().Length];
        _biomeTextures = new Texture2D?[Enum.GetValues<BiomeId>().Length];

        foreach (TerrainId terrain in Enum.GetValues<TerrainId>())
        {
            if (!tiles.Terrain.TryGetValue(terrain.ToString(), out string? fileName))
            {
                continue;
            }

            _terrainTextures[(int)terrain] = TryLoad(content, fileName);
            if (_terrainTextures[(int)terrain] is not null)
            {
                HasTerrainTiles = true;
            }
        }

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            if (!tiles.Biomes.TryGetValue(biome.ToString(), out string? fileName))
            {
                continue;
            }

            _biomeTextures[(int)biome] = TryLoad(content, fileName);
            if (_biomeTextures[(int)biome] is not null)
            {
                HasBiomeTiles = true;
            }
        }
    }

    public Texture2D? GetTerrain(TerrainId terrain)
    {
        int index = (int)terrain;
        return index >= 0 && index < _terrainTextures.Length ? _terrainTextures[index] : null;
    }

    public Texture2D? GetBiome(BiomeId biome)
    {
        int index = (int)biome;
        return index >= 0 && index < _biomeTextures.Length ? _biomeTextures[index] : null;
    }

    public bool SupportsView(GameViewMode viewMode) =>
        viewMode == GameViewMode.Overworld ? HasBiomeTiles : HasTerrainTiles;

    private static Texture2D? TryLoad(ContentManager content, string fileName)
    {
        string assetName = ToAssetName(fileName);
        try
        {
            return content.Load<Texture2D>(assetName);
        }
        catch (Microsoft.Xna.Framework.Content.ContentLoadException ex)
        {
            DebugLog.Issue($"Failed to load tile texture '{assetName}': {ex.Message}");
            return null;
        }
    }

    private static string ToAssetName(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{TilesetRoot}/{stem}";
    }
}
