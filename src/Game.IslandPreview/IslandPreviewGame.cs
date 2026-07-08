using Game.Client;
using Game.Client.Input;
using Game.Client.Presentation;
using Game.Client.Presentation.Camera;
using Game.Client.UI;
using Game.Content;
using Game.IslandPreview.Generation;
using Game.IslandPreview.UI;
using Game.Simulation.Rendering;
using Game.Simulation.World;
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
    private Task<Overworld>? _generationTask;

    public IslandPreviewGame(ulong? initialSeed)
    {
        _initialSeed = initialSeed;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
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
        _camera = new CameraController(_bundle.Camera);
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

        _panel.Update(mouse, _previousMouse, keyboard, _previousKeyboard, GetBackBufferHeight());
        PollGeneration();

        if (_panel.GenerateRequested)
        {
            StartGeneration();
            _panel.ClearGenerateRequest();
        }

        int mapWidth = GetBackBufferWidth() - GenerationParameterPanel.SidebarWidth;
        bool overMap = mouse.X >= GenerationParameterPanel.SidebarWidth;

        if (overMap && mouse.LeftButton == ButtonState.Pressed)
        {
            if (_previousMouse.LeftButton == ButtonState.Released)
            {
                _isDraggingMap = true;
            }
            else if (_isDraggingMap)
            {
                var delta = new Vector2(mouse.X - _previousMouse.X, mouse.Y - _previousMouse.Y);
                _camera.PanByScreenDelta(delta);
            }
        }
        else
        {
            _isDraggingMap = false;
        }

        if (overMap)
        {
            var frame = _inputMapper.Sample();
            frame.MouseX = mouse.X - GenerationParameterPanel.SidebarWidth;
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
        int sidebarWidth = GenerationParameterPanel.SidebarWidth;
        int mapWidth = fullWidth - sidebarWidth;
        int mapHeight = fullHeight;

        GraphicsDevice.Viewport = new Viewport(0, 0, fullWidth, fullHeight);
        GraphicsDevice.Clear(new Color(0x1A, 0x1A, 0x2E));

        if (_snapshot is not null)
        {
            GraphicsDevice.Viewport = new Viewport(sidebarWidth, 0, mapWidth, mapHeight);
            MouseState mouse = Mouse.GetState();
            _renderer.Draw(
                _snapshot,
                _camera,
                _selection,
                _menu,
                uiManager: null,
                mouseX: mouse.X - sidebarWidth,
                mouseY: mouse.Y,
                drawHelpText: false);
        }

        GraphicsDevice.Viewport = new Viewport(0, 0, fullWidth, fullHeight);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _panel.Draw(_spriteBatch, _font, _pixel, fullHeight);

        if (_panel.IsGenerating)
        {
            DrawGeneratingOverlay(sidebarWidth, mapWidth, mapHeight);
        }
        else if (_snapshot is not null)
        {
            _spriteBatch.DrawString(
                _font,
                "Drag to pan | Scroll to zoom | WASD/arrows pan",
                new Vector2(sidebarWidth + 8, 8),
                Color.LightGray);
        }
        else
        {
            _spriteBatch.DrawString(
                _font,
                "Click Generate to create an island.",
                new Vector2(sidebarWidth + 16, 16),
                Color.Gray);
        }

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private int GetBackBufferWidth()
    {
        return GraphicsDevice.PresentationParameters.BackBufferWidth;
    }

    private int GetBackBufferHeight()
    {
        return GraphicsDevice.PresentationParameters.BackBufferHeight;
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
        };

        _panel.SetGenerating(true, "Generating...");
        _cameraCentered = false;
        _generationTask = Task.Run(() => PreviewWorldHost.GenerateWorld(_bundle, request));
    }

    private void PollGeneration()
    {
        if (_generationTask is null)
        {
            return;
        }

        if (!_generationTask.IsCompleted)
        {
            return;
        }

        try
        {
            Overworld world = _generationTask.GetAwaiter().GetResult();
            _worldHost.ApplyGeneratedWorld(world);
            _snapshot = _worldHost.Snapshot;
            if (!_cameraCentered && _snapshot is not null)
            {
                int mapWidth = GetBackBufferWidth() - GenerationParameterPanel.SidebarWidth;
                int mapHeight = GetBackBufferHeight();
                _camera.CenterOn(world.Width / 2f, world.Height / 2f, mapWidth, mapHeight);
                _cameraCentered = true;
            }

            _panel.SetGenerating(false, $"Generated {world.Width}x{world.Height} seed {_panel.Seed}");
        }
        catch (Exception ex)
        {
            _panel.SetGenerating(false, $"Error: {ex.Message}");
        }
        finally
        {
            _generationTask = null;
        }
    }

    private void DrawGeneratingOverlay(int sidebarWidth, int mapWidth, int mapHeight)
    {
        var overlay = new Color(0, 0, 0, 160);
        _spriteBatch.Draw(_pixel, new Rectangle(sidebarWidth, 0, mapWidth, mapHeight), overlay);
        Vector2 textSize = _font.MeasureString("Generating...");
        _spriteBatch.DrawString(
            _font,
            "Generating...",
            new Vector2(sidebarWidth + (mapWidth - textSize.X) / 2f, (mapHeight - textSize.Y) / 2f),
            Color.White);
    }
}
