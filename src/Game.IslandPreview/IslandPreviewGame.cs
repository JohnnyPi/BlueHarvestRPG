using Game.Client;
using Game.Client.Input;
using Game.Client.Presentation;
using Game.Client.Presentation.Camera;
using Game.Client.UI;
using Game.Content;
using Game.Content.Definitions;
using Game.Generation.Island;
using Game.IslandPreview.Generation;
using Game.IslandPreview.UI;
using Game.Simulation.Rendering;
using Game.Simulation.World;
using Game.Simulation.World.Island;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.IslandPreview;

public sealed class IslandPreviewGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly ulong? _initialSeed;

    private GameContentBundle _bundle = null!;
    private PreviewWorldHost _worldHost = null!;
    private GenerationParameterPanel _panel = null!;
    private TileInspectionPanel _inspectionPanel = null!;
    private WorldRenderer _renderer = null!;
    private CameraController _camera = null!;
    private InputMapper _inputMapper = null!;
    private readonly SelectionState _selection = new();
    private readonly ContextMenuController _menu = new();

    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private SpriteFont _font = null!;
    private RenderSnapshot? _snapshot;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private bool _cameraCentered;
    private bool _isDraggingMap;
    private Vector2? _mapPressPosition;
    private const int ClickDragThresholdPixels = 6;
    private Task<Overworld>? _generationTask;
    private Overworld? _generatedWorld;
    private BiomeRulesDefinition? _generatedBiomeRules;
    private IslandGenerationProgressReporter? _generationProgress;
    private FieldOverlayMode _fieldOverlay = FieldOverlayMode.Off;

    private enum FieldOverlayMode
    {
        Off,
        CoastDistance,
        CoastalWidthVariation,
        IslandMask,
        Concavity,
        DistanceToMapEdge,
        BiomeSingletonHeatmap,
        CoastLinearityHeatmap,
        StageDiffLandOcean,
        StageDiffBiome,
        StageDiffElevation,
    }

    private int _snapshotCheckpointIndex;

    public IslandPreviewGame(ulong? initialSeed)
    {
        _initialSeed = initialSeed;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Island Generator Preview";
    }

    protected override void Initialize()
    {
        _bundle = GameBootstrap.LoadContent();
        _worldHost = new PreviewWorldHost(_bundle);
        _panel = new GenerationParameterPanel(_bundle.Island, _bundle.BiomeRules, _initialSeed);
        _inspectionPanel = new TileInspectionPanel();
        _camera = new CameraController(_bundle.Camera, minZoomOverride: 0.04f);
        _inputMapper = new InputMapper(_bundle.Controls);
        _previousMouse = Mouse.GetState();
        _previousKeyboard = Keyboard.GetState();

        Window.TextInput += (_, args) => _panel.HandleTextInput(args.Character);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _renderer = new WorldRenderer(GraphicsDevice, Content, _bundle);
        _renderer.LoadContent();
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _font = _renderer.MenuFont ?? throw new InvalidOperationException("Menu font failed to load.");
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        MouseState mouse = Mouse.GetState();
        KeyboardState keyboard = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.F2) && _previousKeyboard.IsKeyUp(Keys.F2))
        {
            _fieldOverlay = (FieldOverlayMode)(((int)_fieldOverlay + 1) % Enum.GetValues<FieldOverlayMode>().Length);
        }

        if (keyboard.IsKeyDown(Keys.F3) && _previousKeyboard.IsKeyUp(Keys.F3)
            && _generatedWorld?.IslandPlan?.GenerationSnapshots.Count > 0)
        {
            int count = _generatedWorld.IslandPlan.GenerationSnapshots.Count;
            _snapshotCheckpointIndex = (_snapshotCheckpointIndex + 1) % count;
        }

        _panel.Update(mouse, _previousMouse, keyboard, _previousKeyboard, GetBackBufferHeight());
        UpdateGenerationStatus();
        PollGeneration();

        if (_panel.GenerateRequested)
        {
            StartGeneration();
            _panel.ClearGenerateRequest();
        }

        bool overMap = IsOverMapDynamic(mouse.X);

        if (overMap && mouse.LeftButton == ButtonState.Pressed)
        {
            if (_previousMouse.LeftButton == ButtonState.Released)
            {
                _mapPressPosition = new Vector2(mouse.X, mouse.Y);
                _isDraggingMap = false;
            }
            else if (_mapPressPosition is Vector2 pressPosition)
            {
                var deltaFromPress = new Vector2(mouse.X, mouse.Y) - pressPosition;
                if (!_isDraggingMap && deltaFromPress.LengthSquared() > ClickDragThresholdPixels * ClickDragThresholdPixels)
                {
                    _isDraggingMap = true;
                }

                if (_isDraggingMap)
                {
                    var delta = new Vector2(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y);
                    _camera.PanByScreenDelta(delta);
                }
            }
        }
        else if (_previousMouse.LeftButton == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released)
        {
            if (overMap && !_isDraggingMap)
            {
                TrySelectTileAt(mouse.X, mouse.Y);
            }

            _isDraggingMap = false;
            _mapPressPosition = null;
        }
        else
        {
            _isDraggingMap = false;
            _mapPressPosition = null;
        }

        if (overMap)
        {
            var frame = _inputMapper.Sample();
            frame.MouseX = mouse.X - PreviewLayout.LeftSidebarWidth;
            frame.MouseY = mouse.Y;
            _camera.Update(frame, deltaSeconds);
        }
        else
        {
            _inputMapper.Sample();
        }

        _previousMouse = mouse;
        _previousKeyboard = keyboard;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        int fullWidth = GetBackBufferWidth();
        int fullHeight = GetBackBufferHeight();
        int mapLeft = PreviewLayout.LeftSidebarWidth;
        int mapWidth = GetMapWidth();
        int mapHeight = fullHeight;

        GraphicsDevice.Viewport = new Viewport(0, 0, fullWidth, fullHeight);
        GraphicsDevice.Clear(new Color(0x1A, 0x1A, 0x2E));

        if (_snapshot is not null)
        {
            GraphicsDevice.Viewport = new Viewport(mapLeft, 0, mapWidth, mapHeight);
            MouseState mouse = Mouse.GetState();
            _renderer.Draw(
                _snapshot,
                _camera,
                _selection,
                _menu,
                uiManager: null,
                mouseX: mouse.X - mapLeft,
                mouseY: mouse.Y,
                drawHelpText: false);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawFieldOverlay(mapWidth, mapHeight);
            DrawSelectedTileHighlight(mapWidth, mapHeight);
            _spriteBatch.End();
        }

        GraphicsDevice.Viewport = new Viewport(0, 0, fullWidth, fullHeight);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _panel.Draw(_spriteBatch, _font, _pixel, fullHeight);
        _inspectionPanel.Draw(_spriteBatch, _font, _pixel, fullWidth, fullHeight);

        if (_panel.IsGenerating)
        {
            DrawGeneratingOverlay(mapLeft, mapWidth, mapHeight);
        }
        else if (_snapshot is not null)
        {
            _spriteBatch.DrawString(
                _font,
                "Drag to pan | Scroll to zoom | Click tile to inspect | F2 overlay | F3 snapshot",
                new Vector2(mapLeft + 8, 8),
                Color.LightGray);
            if (_fieldOverlay != FieldOverlayMode.Off)
            {
                _spriteBatch.DrawString(
                    _font,
                    $"Overlay: {_fieldOverlay}",
                    new Vector2(mapLeft + 8, 28),
                    Color.Cyan);
            }
        }
        else
        {
            _spriteBatch.DrawString(
                _font,
                "Click Generate to create an island.",
                new Vector2(mapLeft + 16, 16),
                Color.Gray);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private int GetBackBufferWidth() => GraphicsDevice.PresentationParameters.BackBufferWidth;

    private int GetBackBufferHeight() => GraphicsDevice.PresentationParameters.BackBufferHeight;

    private int GetMapWidth() =>
        GetBackBufferWidth() - PreviewLayout.LeftSidebarWidth - PreviewLayout.RightSidebarWidth;

    private bool IsOverMapDynamic(int screenX)
    {
        return screenX >= PreviewLayout.LeftSidebarWidth
            && screenX < GetBackBufferWidth() - PreviewLayout.RightSidebarWidth;
    }

    private void StartGeneration()
    {
        if (_generationTask is { IsCompleted: false })
        {
            return;
        }

        var request = new PreviewGenerationRequest
        {
            Island = _panel.CloneIslandDefinition(),
            BiomeRules = _panel.CloneBiomeRules(),
            Seed = _panel.Seed,
            Progress = _generationProgress = new IslandGenerationProgressReporter(),
        };
        _generatedBiomeRules = request.BiomeRules;

        _panel.SetGenerating(true, "Starting...");
        _inspectionPanel.ClearTileInspection();
        _cameraCentered = false;
        _generationTask = Task.Run(() => PreviewWorldHost.GenerateWorld(_bundle, request));
    }

    private void UpdateGenerationStatus()
    {
        if (_generationTask is null || _generationTask.IsCompleted || _generationProgress is null)
        {
            return;
        }

        IslandGenerationProgressSnapshot snapshot = _generationProgress.GetSnapshot();
        _panel.SetGenerating(true, snapshot.BriefStatus);
    }

    private void PollGeneration()
    {
        if (_generationTask is null || !_generationTask.IsCompleted)
        {
            return;
        }

        try
        {
            Overworld world = _generationTask.GetAwaiter().GetResult();
            _generatedWorld = world;
            _worldHost.ApplyGeneratedWorld(world, _generatedBiomeRules);
            _snapshot = _worldHost.Snapshot;
            if (!_cameraCentered && _snapshot is not null)
            {
                _camera.CenterOn(world.Width / 2f, world.Height / 2f, GetMapWidth(), GetBackBufferHeight());
                _cameraCentered = true;
            }

            string completionStatus = BuildCompletionStatus(world);
            _panel.SetGenerating(false, completionStatus);
        }
        catch (Exception ex)
        {
            _panel.SetGenerating(false, $"Error: {ex.Message}");
        }
        finally
        {
            _generationTask = null;
            _generationProgress = null;
        }
    }

    private string BuildCompletionStatus(Overworld world)
    {
        if (_generationProgress is null)
        {
            return $"Generated {world.Width}x{world.Height} seed {_panel.Seed}";
        }

        IslandGenerationProgressSnapshot snapshot = _generationProgress.GetSnapshot();
        IslandGenerationDiagnostics? diagnostics = world.IslandPlan?.GenerationDiagnostics;
        if (diagnostics is null || diagnostics.AttemptedShapeScales.Count == 0)
        {
            return $"Generated {world.Width}x{world.Height} in {IslandGenerationProgressSnapshot.FormatSeconds(snapshot.TotalElapsedMs)}";
        }

        string frameStatus = diagnostics.OceanFramePassed ? "PASS" : "FAIL";
        return $"Frame {frameStatus} | scale {diagnostics.SelectedShapeScale:0.000} "
            + $"crop ({diagnostics.CropOffsetX},{diagnostics.CropOffsetY}) "
            + $"land {diagnostics.CroppedLandCoverage:P1} "
            + $"beach {diagnostics.MinObservedBeachWidth:0.000}-{diagnostics.MaxObservedBeachWidth:0.000} "
            + $"shallow {diagnostics.MinObservedShallowWaterWidth:0.000}-{diagnostics.MaxObservedShallowWaterWidth:0.000} "
            + $"violations L{diagnostics.LandFrameViolations}/C{diagnostics.CoastFrameViolations} "
            + $"run {diagnostics.MaxAxisAlignedCoastRun} | "
            + IslandGenerationProgressSnapshot.FormatSeconds(snapshot.TotalElapsedMs);
    }

    private void DrawGeneratingOverlay(int mapLeft, int mapWidth, int mapHeight)
    {
        var overlay = new Color(0, 0, 0, 160);
        _spriteBatch.Draw(_pixel, new Rectangle(mapLeft, 0, mapWidth, mapHeight), overlay);

        IslandGenerationProgressSnapshot snapshot = _generationProgress?.GetSnapshot()
            ?? new IslandGenerationProgressSnapshot(null, 0, 0, Array.Empty<IslandGenerationStageTiming>());

        const int lineHeight = 18;
        const int maxCompletedLines = 10;
        var lines = new List<string> { "Generating island..." };

        if (snapshot.CurrentStage is not null)
        {
            lines.Add($"> {snapshot.CurrentStage} ({IslandGenerationProgressSnapshot.FormatSeconds(snapshot.CurrentStageElapsedMs)})");
        }

        int completedStart = Math.Max(0, snapshot.CompletedStages.Count - maxCompletedLines);
        for (int i = completedStart; i < snapshot.CompletedStages.Count; i++)
        {
            IslandGenerationStageTiming stage = snapshot.CompletedStages[i];
            lines.Add($"  {stage.Name}  {IslandGenerationProgressSnapshot.FormatDuration(stage.DurationMs)}");
        }

        lines.Add($"Total {IslandGenerationProgressSnapshot.FormatSeconds(snapshot.TotalElapsedMs)}");

        float maxWidth = 0;
        foreach (string line in lines)
        {
            maxWidth = MathF.Max(maxWidth, _font.MeasureString(line).X);
        }

        float blockHeight = lines.Count * lineHeight;
        float startX = mapLeft + (mapWidth - maxWidth) / 2f;
        float startY = (mapHeight - blockHeight) / 2f;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            Color color = i == 0
                ? Color.White
                : i == 1 && snapshot.CurrentStage is not null
                    ? Color.Gold
                    : Color.LightGray;
            _spriteBatch.DrawString(_font, line, new Vector2(startX, startY + i * lineHeight), color);
        }
    }

    private void TrySelectTileAt(int screenX, int screenY)
    {
        if (_generatedWorld?.IslandPlan is not IslandPlan plan)
        {
            return;
        }

        if (!IsOverMapDynamic(screenX))
        {
            return;
        }

        int mapX = screenX - PreviewLayout.LeftSidebarWidth;
        int mapY = screenY;
        if (!GridPicker.TryScreenToGrid(_camera, mapX, mapY, plan.Width, plan.Height, out int tileX, out int tileY))
        {
            return;
        }

        _inspectionPanel.SetTileInspection(plan, tileX, tileY);
    }

    private void DrawSelectedTileHighlight(int mapWidth, int mapHeight)
    {
        if (_inspectionPanel.SelectedTile is not (int tileX, int tileY))
        {
            return;
        }

        if (!GridPicker.TryGetTileScreenBounds(_camera, tileX, tileY, out Rectangle bounds))
        {
            return;
        }

        if (bounds.Right < 0 || bounds.Bottom < 0 || bounds.X > mapWidth || bounds.Y > mapHeight)
        {
            return;
        }

        var highlight = new Color(0xFF, 0xD7, 0x00, 180);
        int border = Math.Max(1, bounds.Width / 8);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, border), highlight);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - border, bounds.Width, border), highlight);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, border, bounds.Height), highlight);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - border, bounds.Y, border, bounds.Height), highlight);
    }

    private void DrawFieldOverlay(int mapWidth, int mapHeight)
    {
        if (_fieldOverlay == FieldOverlayMode.Off || _generatedWorld?.IslandPlan is not IslandPlan plan)
        {
            return;
        }

        float[]? field = ResolveOverlayField(plan);
        if (field is null || field.Length != plan.Width * plan.Height)
        {
            return;
        }

        DrawNormalizedFieldOverlay(plan, mapWidth, mapHeight, field);
    }

    private float[]? ResolveOverlayField(IslandPlan plan)
    {
        return _fieldOverlay switch
        {
            FieldOverlayMode.CoastDistance => plan.CoastDistance,
            FieldOverlayMode.CoastalWidthVariation => plan.CoastalWidthVariation,
            FieldOverlayMode.IslandMask => plan.IslandMask,
            FieldOverlayMode.Concavity => plan.Concavity,
            FieldOverlayMode.DistanceToMapEdge => IslandQualityMetrics.ComputeDistanceToMapEdgeField(plan),
            FieldOverlayMode.BiomeSingletonHeatmap => IslandQualityMetrics.ComputeBiomeSingletonHeatmap(plan),
            FieldOverlayMode.CoastLinearityHeatmap => IslandQualityMetrics.ComputeCoastLinearityHeatmap(plan),
            FieldOverlayMode.StageDiffLandOcean => IslandQualityMetrics.ComputeStageDiffHeatmap(
                GetSelectedSnapshot(plan), plan, StageDiffMode.LandOcean),
            FieldOverlayMode.StageDiffBiome => IslandQualityMetrics.ComputeStageDiffHeatmap(
                GetSelectedSnapshot(plan), plan, StageDiffMode.Biome),
            FieldOverlayMode.StageDiffElevation => IslandQualityMetrics.ComputeStageDiffHeatmap(
                GetSelectedSnapshot(plan), plan, StageDiffMode.Elevation),
            _ => null,
        };
    }

    private IslandGenerationSnapshot? GetSelectedSnapshot(IslandPlan plan)
    {
        if (plan.GenerationSnapshots.Count == 0)
        {
            return null;
        }

        int index = Math.Clamp(_snapshotCheckpointIndex, 0, plan.GenerationSnapshots.Count - 1);
        return plan.GenerationSnapshots[index];
    }

    private void DrawNormalizedFieldOverlay(IslandPlan plan, int mapWidth, int mapHeight, float[] field)
    {

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float value in field)
        {
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
        }

        float range = MathF.Max(0.0001f, max - min);
        Vector2 topLeft = _camera.ScreenToWorld(Vector2.Zero);
        Vector2 bottomRight = _camera.ScreenToWorld(new Vector2(mapWidth, mapHeight));
        int startX = Math.Clamp((int)MathF.Floor(topLeft.X), 0, plan.Width - 1);
        int startY = Math.Clamp((int)MathF.Floor(topLeft.Y), 0, plan.Height - 1);
        int endX = Math.Clamp((int)MathF.Ceiling(bottomRight.X), 0, plan.Width);
        int endY = Math.Clamp((int)MathF.Ceiling(bottomRight.Y), 0, plan.Height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                if (!GridPicker.TryGetTileScreenBounds(_camera, x, y, out Rectangle bounds))
                {
                    continue;
                }

                float normalized = (field[y * plan.Width + x] - min) / range;
                byte channel = (byte)Math.Clamp((int)(normalized * 255f), 0, 255);
                var color = new Color(channel, (byte)(255 - channel), (byte)180, (byte)120);
                _spriteBatch.Draw(_pixel, bounds, color);
            }
        }
    }
}
