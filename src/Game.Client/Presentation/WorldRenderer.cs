using Game.Client.Presentation.Camera;
using Game.Client.UI;
using Game.Content;
using Game.Content.Definitions;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SimTerrain = Game.Simulation.LocalMaps.TerrainId;

namespace Game.Client.Presentation;

public sealed class WorldRenderer
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ContentManager _content;
    private readonly GameContentBundle _bundle;

    private readonly Color[] _biomeColors = new Color[Enum.GetValues<BiomeId>().Length];
    private readonly Color[] _terrainColors = new Color[Enum.GetValues<SimTerrain>().Length];
    private readonly Color _selectionColor;
    private readonly Color _playerColor;
    private readonly Color _borderShadeColor = new(0, 0, 0, 110);
    private readonly Color _creatureColor = new(0xE9, 0x45, 0x60);
    private readonly Color _treeEntityColor = new(0x2E, 0xCC, 0x71);
    private readonly Color _landmarkColor = new(0xDD, 0xDD, 0xEE);
    private readonly Color _escapeLandmarkColor = new(0xFF, 0xD7, 0x00);
    private readonly Color _mysteryLandmarkColor = new(0x5D, 0xCB, 0xFF);
    private readonly Color _unseenColor = new(0, 0, 0, 255);
    private readonly Color _exploredDimColor = new(0x22, 0x22, 0x33, 255);
    private readonly Color _hazardTravelColor = new(0xAA, 0x33, 0x33, 220);

    public SpriteFont? MenuFont => _font;
    public UiPainter? Painter => _uiPainter;

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private SpriteFont? _font;
    private UiPainter? _uiPainter;
    private UiThemeColors? _theme;

    public WorldRenderer(GraphicsDevice graphicsDevice, ContentManager content, GameContentBundle bundle)
    {
        _graphicsDevice = graphicsDevice;
        _content = content;
        _bundle = bundle;
        _theme = new UiThemeColors(bundle.UiTheme);

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            _biomeColors[(int)biome] = LookupColor(_bundle.BiomeColors.Biomes, biome.ToString(), Color.Magenta);
        }

        foreach (SimTerrain terrain in Enum.GetValues<SimTerrain>())
        {
            _terrainColors[(int)terrain] = LookupColor(_bundle.TerrainColors.Terrain, terrain.ToString(), Color.Magenta);
        }

        _selectionColor = ColorParser.Parse(_bundle.Camera.SelectionColor, Color.Gold);
        _playerColor = ColorParser.Parse(_bundle.Camera.PlayerColor, Color.Yellow);
    }

    private static Color LookupColor(Dictionary<string, ColorEntry> map, string key, Color fallback)
    {
        return map.TryGetValue(key, out ColorEntry? entry)
            ? ColorParser.Parse(entry.Color, fallback)
            : fallback;
    }

    public void LoadContent()
    {
        _spriteBatch = new SpriteBatch(_graphicsDevice);
        _pixel = new Texture2D(_graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        try
        {
            _font = _content.Load<SpriteFont>("Fonts/Arial");
        }
        catch (Microsoft.Xna.Framework.Content.ContentLoadException)
        {
            _font = CreateFallbackFont();
        }

        _uiPainter = new UiPainter(_spriteBatch, _pixel, _font);
    }

    public void Draw(
        RenderSnapshot snapshot,
        CameraController camera,
        SelectionState selection,
        ContextMenuController menu,
        UiManager? uiManager,
        int mouseX,
        int mouseY)
    {
        _graphicsDevice.Clear(new Color(0x1A, 0x1A, 0x2E));
        _spriteBatch.Begin();

        DrawWorld(snapshot, camera, selection);

        if (_font is not null)
        {
            var viewport = _graphicsDevice.Viewport;
            _spriteBatch.DrawString(
                _font,
                "HJKL/numpad move | WASD/arrows pan | Space wait | I inventory | U character | Esc menu | F5 save | Right-click context",
                new Vector2(8, 8),
                Color.LightGray);

            int logY = 22;
            foreach (string line in snapshot.MessageLog)
            {
                _spriteBatch.DrawString(_font, line, new Vector2(8, logY), Color.Cyan);
                logY += 16;
            }
        }

        if (_uiPainter is not null && uiManager is not null)
        {
            uiManager.Draw(_uiPainter, snapshot, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, mouseX, mouseY);
        }

        if (menu.IsOpen && _uiPainter is not null && _theme is not null)
        {
            DrawMenu(_uiPainter, _theme, menu, mouseX, mouseY);
        }

        _spriteBatch.End();
    }

    private void DrawWorld(RenderSnapshot snapshot, CameraController camera, SelectionState selection)
    {
        float cellSize = camera.CellSize;
        var viewport = _graphicsDevice.Viewport;

        int minGridX = Math.Max(0, (int)Math.Floor(camera.ScreenToWorld(new Vector2(0, 0)).X));
        int minGridY = Math.Max(0, (int)Math.Floor(camera.ScreenToWorld(new Vector2(0, 0)).Y));
        int maxGridX = Math.Min(snapshot.GridWidth - 1, (int)Math.Ceiling(camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height)).X));
        int maxGridY = Math.Min(snapshot.GridHeight - 1, (int)Math.Ceiling(camera.ScreenToWorld(new Vector2(viewport.Width, viewport.Height)).Y));

        int cellPixel = Math.Max(1, (int)cellSize - 1);

        for (int y = minGridY; y <= maxGridY; y++)
        {
            for (int x = minGridX; x <= maxGridX; x++)
            {
                int index = y * snapshot.GridWidth + x;
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

                Vector2 screen = camera.WorldToScreen(x, y);
                DrawRect((int)screen.X, (int)screen.Y, cellPixel, cellPixel, color);

                if (snapshot.ViewMode == GameViewMode.LocalMap && MapBorderHelper.IsBorderTile(x, y))
                {
                    DrawRect((int)screen.X, (int)screen.Y, cellPixel, cellPixel, _borderShadeColor);
                }
            }
        }

        if (snapshot.ViewMode == GameViewMode.Overworld)
        {
            DrawOverworldGeology(snapshot, camera, cellPixel, minGridX, minGridY, maxGridX, maxGridY);
            DrawOverworldLandmarks(snapshot, camera, cellPixel, cellSize);
        }

        foreach (EntityRenderData entity in snapshot.Entities)
        {
            if (snapshot.VisibleTiles is not null)
            {
                int index = entity.Y * snapshot.GridWidth + entity.X;
                if (index >= 0 && index < snapshot.VisibleTiles.Length && !snapshot.VisibleTiles[index])
                {
                    continue;
                }
            }

            Color entityColor = entity.Kind switch
            {
                (int)EntityKind.WanderingCreature => _creatureColor,
                (int)EntityKind.Raptor => new Color(0xFF, 0x6B, 0x35),
                (int)EntityKind.HarvestableTree => _treeEntityColor,
                _ => Color.White
            };

            Vector2 entityScreen = camera.WorldToScreen(entity.X, entity.Y);
            DrawRect((int)entityScreen.X, (int)entityScreen.Y, cellPixel, cellPixel, entityColor);
        }

        Vector2 playerScreen = camera.WorldToScreen(snapshot.PlayerX, snapshot.PlayerY);
        DrawRect((int)playerScreen.X, (int)playerScreen.Y, cellPixel, cellPixel, _playerColor);

        if (selection.IsLocked && selection.ViewModeWhenLocked == snapshot.ViewMode)
        {
            Vector2 selScreen = camera.WorldToScreen(selection.TileX, selection.TileY);
            DrawBorder((int)selScreen.X, (int)selScreen.Y, (int)cellSize, (int)cellSize, _selectionColor, 2);
        }
    }

    private void DrawOverworldGeology(
        RenderSnapshot snapshot,
        CameraController camera,
        int cellPixel,
        int minGridX,
        int minGridY,
        int maxGridX,
        int maxGridY)
    {
        int lineThickness = Math.Max(1, cellPixel / 4);

        for (int y = minGridY; y <= maxGridY; y++)
        {
            for (int x = minGridX; x <= maxGridX; x++)
            {
                int index = y * snapshot.GridWidth + x;
                if (snapshot.ExploredTiles is not null && !snapshot.ExploredTiles[index])
                {
                    continue;
                }

                Vector2 screen = camera.WorldToScreen(x, y);
                int screenX = (int)screen.X;
                int screenY = (int)screen.Y;

                if (snapshot.TectonicBoundaries is not null)
                {
                    Color? boundaryColor = BoundaryColor(snapshot.TectonicBoundaries[index]);
                    if (boundaryColor.HasValue)
                    {
                        DrawBorder(screenX, screenY, cellPixel, cellPixel, boundaryColor.Value, lineThickness);
                    }
                }

                if (snapshot.RiverEdgeMask is not null && snapshot.RiverEdgeMask[index] != 0)
                {
                    DrawRiverEdges(screenX, screenY, cellPixel, snapshot.RiverEdgeMask[index], lineThickness);
                }
            }
        }
    }

    private static Color? BoundaryColor(byte boundaryType) => OverworldGeologyColors.ForBoundaryType(boundaryType);

    private void DrawRiverEdges(int screenX, int screenY, int cellPixel, byte edgeMask, int thickness)
    {
        const byte north = 1;
        const byte east = 2;
        const byte south = 4;
        const byte west = 8;

        if ((edgeMask & north) != 0)
        {
            DrawRect(screenX, screenY, cellPixel, thickness, OverworldGeologyColors.River);
        }

        if ((edgeMask & east) != 0)
        {
            DrawRect(screenX + cellPixel - thickness, screenY, thickness, cellPixel, OverworldGeologyColors.River);
        }

        if ((edgeMask & south) != 0)
        {
            DrawRect(screenX, screenY + cellPixel - thickness, cellPixel, thickness, OverworldGeologyColors.River);
        }

        if ((edgeMask & west) != 0)
        {
            DrawRect(screenX, screenY, thickness, cellPixel, OverworldGeologyColors.River);
        }
    }

    private void DrawOverworldLandmarks(RenderSnapshot snapshot, CameraController camera, int cellPixel, float cellSize)
    {
        if (_font is null || snapshot.OverworldLandmarks is not { Length: > 0 })
        {
            return;
        }

        foreach (OverworldLandmarkView landmark in snapshot.OverworldLandmarks)
        {
            if (landmark.X < 0 || landmark.Y < 0 ||
                landmark.X >= snapshot.GridWidth || landmark.Y >= snapshot.GridHeight)
            {
                continue;
            }

            Vector2 screen = camera.WorldToScreen(landmark.X, landmark.Y);
            Color markerColor = landmark.ObjectiveKind switch
            {
                OverworldLandmarkObjectiveKind.Escape => _escapeLandmarkColor,
                OverworldLandmarkObjectiveKind.Mystery => _mysteryLandmarkColor,
                _ => _landmarkColor
            };

            int markerSize = Math.Max(2, cellPixel / 2);
            int markerX = (int)screen.X + cellPixel / 2 - markerSize / 2;
            int markerY = (int)screen.Y + cellPixel / 2 - markerSize / 2;
            DrawRect(markerX, markerY, markerSize, markerSize, markerColor);

            if (cellSize >= 8)
            {
                string label = landmark.ObjectiveKind switch
                {
                    OverworldLandmarkObjectiveKind.Escape => $"[{landmark.Name}]",
                    OverworldLandmarkObjectiveKind.Mystery => $"?{landmark.Name}",
                    _ => landmark.Name
                };

                int labelX = (int)screen.X + cellPixel + 1;
                int labelY = (int)screen.Y - 2;
                _spriteBatch.DrawString(_font, label, new Vector2(labelX, labelY), markerColor);
            }
        }
    }

    private Color SafeBiomeColor(ushort value)
    {
        return value < _biomeColors.Length ? _biomeColors[value] : Color.Magenta;
    }

    private Color SafeTerrainColor(ushort value)
    {
        return value < _terrainColors.Length ? _terrainColors[value] : Color.Magenta;
    }

    private SpriteFont CreateFallbackFont()
    {
        const int glyphWidth = 8;
        const int glyphHeight = 12;
        const int glyphCount = 96;
        var texture = new Texture2D(_graphicsDevice, glyphWidth * glyphCount, glyphHeight);
        var data = new Color[glyphWidth * glyphCount * glyphHeight];

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Color.White;
        }

        texture.SetData(data);

        var glyphs = new List<Rectangle>(glyphCount);
        var cropping = new List<Rectangle>(glyphCount);
        var kerning = new List<Vector3>(glyphCount);
        var characterMap = new List<char>(glyphCount);

        for (int i = 0; i < glyphCount; i++)
        {
            char c = (char)(32 + i);
            characterMap.Add(c);
            glyphs.Add(new Rectangle(i * glyphWidth, 0, glyphWidth, glyphHeight));
            cropping.Add(new Rectangle(0, 0, glyphWidth, glyphHeight));
            kerning.Add(Vector3.Zero);
        }

        return new SpriteFont(texture, glyphs, cropping, characterMap, glyphHeight, 0, kerning, '?');
    }

    private static void DrawMenu(UiPainter painter, UiThemeColors theme, ContextMenuController menu, int mouseX, int mouseY)
    {
        painter.DrawRect(menu.ScreenX, menu.ScreenY, menu.MenuWidth, menu.MenuHeight, theme.PanelBackground);
        painter.DrawBorder(menu.ScreenX, menu.ScreenY, menu.MenuWidth, menu.MenuHeight, theme.PanelBorder, 1);

        int hovered = menu.HoveredIndex(mouseX, mouseY);
        for (int i = 0; i < menu.Entries.Count; i++)
        {
            int entryY = menu.ScreenY + ContextMenuController.Padding + i * ContextMenuController.EntryHeight;
            if (i == hovered)
            {
                painter.DrawRect(menu.ScreenX + 2, entryY + 1, menu.MenuWidth - 4, ContextMenuController.EntryHeight - 2, theme.PanelAccent);
            }

            if (i > 0)
            {
                painter.DrawRect(
                    menu.ScreenX + ContextMenuController.TextPaddingX,
                    entryY,
                    menu.MenuWidth - ContextMenuController.TextPaddingX * 2,
                    1,
                    theme.PanelDivider);
            }

            painter.DrawString(
                menu.Entries[i].Label,
                menu.ScreenX + ContextMenuController.TextPaddingX,
                entryY + ContextMenuController.TextPaddingY,
                theme.TextPrimary);
        }
    }

    private void DrawRect(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }

    private void DrawBorder(int x, int y, int width, int height, Color color, int thickness)
    {
        DrawRect(x, y, width, thickness, color);
        DrawRect(x, y + height - thickness, width, thickness, color);
        DrawRect(x, y, thickness, height, color);
        DrawRect(x + width - thickness, y, thickness, height, color);
    }
}
