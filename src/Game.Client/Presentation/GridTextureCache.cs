using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SimTerrain = Game.Simulation.LocalMaps.TerrainId;

namespace Game.Client.Presentation;

internal sealed class GridTextureCache
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Color[] _biomeColors;
    private readonly Color[] _terrainColors;
    private readonly Color _unseenColor;
    private readonly Color _exploredDimColor;
    private readonly Color _hazardTravelColor;

    private Texture2D? _texture;
    private ushort[]? _sourceCellData;
    private bool[]? _sourceVisibleTiles;
    private bool[]? _sourceExploredTiles;
    private GameViewMode _sourceViewMode;
    private int? _sourceHazardX;
    private int? _sourceHazardY;
    private Color[]? _pixelBuffer;

    public GridTextureCache(
        GraphicsDevice graphicsDevice,
        Color[] biomeColors,
        Color[] terrainColors,
        Color unseenColor,
        Color exploredDimColor,
        Color hazardTravelColor)
    {
        _graphicsDevice = graphicsDevice;
        _biomeColors = biomeColors;
        _terrainColors = terrainColors;
        _unseenColor = unseenColor;
        _exploredDimColor = exploredDimColor;
        _hazardTravelColor = hazardTravelColor;
    }

    public Texture2D GetOrBuild(RenderSnapshot snapshot)
    {
        if (_texture is not null &&
            ReferenceEquals(_sourceCellData, snapshot.CellData) &&
            ReferenceEquals(_sourceVisibleTiles, snapshot.VisibleTiles) &&
            ReferenceEquals(_sourceExploredTiles, snapshot.ExploredTiles) &&
            _sourceViewMode == snapshot.ViewMode &&
            _sourceHazardX == snapshot.HazardousTravelX &&
            _sourceHazardY == snapshot.HazardousTravelY)
        {
            return _texture;
        }

        Rebuild(snapshot);
        return _texture!;
    }

    private void Rebuild(RenderSnapshot snapshot)
    {
        int width = snapshot.GridWidth;
        int height = snapshot.GridHeight;
        int size = width * height;

        _pixelBuffer ??= new Color[size];
        if (_pixelBuffer.Length != size)
        {
            _pixelBuffer = new Color[size];
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                ushort cellValue = snapshot.CellData[index];
                Color color = snapshot.ViewMode == GameViewMode.Overworld
                    ? SafeBiomeColor(cellValue)
                    : SafeTerrainColor(cellValue);

                if (snapshot.ViewMode == GameViewMode.LocalMap &&
                    snapshot.VisibleTiles is not null &&
                    snapshot.ExploredTiles is not null)
                {
                    if (!snapshot.ExploredTiles[index])
                    {
                        color = _unseenColor;
                    }
                    else if (!snapshot.VisibleTiles[index])
                    {
                        color = Color.Lerp(color, _exploredDimColor, 0.65f);
                    }
                }
                else if (snapshot.ViewMode == GameViewMode.Overworld &&
                         snapshot.ExploredTiles is not null &&
                         !snapshot.ExploredTiles[index])
                {
                    color = _unseenColor;
                }
                else if (snapshot.ViewMode == GameViewMode.Overworld &&
                         snapshot.HazardousTravelX == x &&
                         snapshot.HazardousTravelY == y)
                {
                    color = Color.Lerp(color, _hazardTravelColor, 0.55f);
                }

                _pixelBuffer[index] = color;
            }
        }

        if (_texture is null || _texture.Width != width || _texture.Height != height)
        {
            _texture?.Dispose();
            _texture = new Texture2D(_graphicsDevice, width, height);
        }

        _texture.SetData(_pixelBuffer);
        _sourceCellData = snapshot.CellData;
        _sourceVisibleTiles = snapshot.VisibleTiles;
        _sourceExploredTiles = snapshot.ExploredTiles;
        _sourceViewMode = snapshot.ViewMode;
        _sourceHazardX = snapshot.HazardousTravelX;
        _sourceHazardY = snapshot.HazardousTravelY;
    }

    private Color SafeBiomeColor(ushort value)
    {
        return value < _biomeColors.Length ? _biomeColors[value] : Color.Magenta;
    }

    private Color SafeTerrainColor(ushort value)
    {
        return value < _terrainColors.Length ? _terrainColors[value] : Color.Magenta;
    }
}
