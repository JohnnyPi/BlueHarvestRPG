using Game.Client.Debugging;
using Game.Client.Input;
using System.Diagnostics;
using Game.Client.Presentation;
using Game.Client.Presentation.Camera;
using Game.Client.UI;
using Game.Content;
using Game.Content.Definitions;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Input;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Microsoft.Xna.Framework;

namespace Game.Client;

public sealed class BlueHarvestGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameContentBundle _content;
    private readonly SimulationHost _simulation;
    private readonly SaveManager _saveManager;
    private readonly InputMapper _inputMapper;
    private readonly CameraController _camera;
    private readonly SelectionState _selection = new();
    private readonly ContextMenuController _menu;
    private readonly UiManager _uiManager;

    private WorldRenderer _renderer = null!;
    private RenderSnapshot _snapshot = null!;
    private bool _cameraCentered;
    private int _lastPlayerX = -1;
    private int _lastPlayerY = -1;
    private readonly DebugFrameStats _debugStats = new();
    private readonly Stopwatch _frameStopwatch = new();
    private int _hoverGridX = -1;
    private int _hoverGridY = -1;
    private GameViewMode _hoverViewMode = GameViewMode.Overworld;

    public BlueHarvestGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Blue Harvest";

        _content = GameBootstrap.LoadContent();
        _saveManager = GameBootstrap.CreateSaveManager();
        DebugLog.Initialize(_saveManager.SaveDirectory);
        DebugContentValidator.Validate(_content);
        _simulation = GameBootstrap.CreateSimulationHost(_content, _saveManager.SaveDirectory);
        _inputMapper = new InputMapper(_content.Controls);
        _camera = new CameraController(_content.Camera);
        _menu = new ContextMenuController();
        _uiManager = new UiManager(_content, _saveManager, _simulation, Exit);
    }

    protected override void Initialize()
    {
        _simulation.Initialize();
        _snapshot = _simulation.BuildRenderSnapshot();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _renderer = new WorldRenderer(GraphicsDevice, Content, _content);
        _renderer.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        _debugStats.BeginFrame();
        _frameStopwatch.Restart();

        float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        InputFrame frame = _inputMapper.Sample();

        if (frame.Pressed.Contains(InputAction.ToggleDebug))
        {
            DebugMode.Toggle();
            DebugLog.Info(DebugMode.IsEnabled ? "Debug mode enabled." : "Debug mode disabled.");
        }

        if (DebugMode.IsEnabled && frame.Pressed.Contains(InputAction.RevealMap))
        {
            _simulation.Session.RevealEntireOverworld();
            DebugLog.Info("Debug reveal: entire overworld explored.");
        }

        if (!_cameraCentered)
        {
            _camera.CenterOn(_snapshot.PlayerX, _snapshot.PlayerY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _cameraCentered = true;
            _lastPlayerX = _snapshot.PlayerX;
            _lastPlayerY = _snapshot.PlayerY;
        }

        _camera.Update(frame, deltaSeconds);

        if (frame.Pressed.Contains(InputAction.RecenterCamera))
        {
            _camera.CenterOn(_snapshot.PlayerX, _snapshot.PlayerY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        }

        UpdateHoverTooltip(frame);

        HandleMenuAndSelection(frame, out bool menuClosedThisFrame);

        bool blockSimulation = _menu.IsOpen || _uiManager.ShouldBlockSimulation;
        _uiManager.HandleInput(frame, _snapshot, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, _menu.IsOpen, menuClosedThisFrame);
        HandleSimulationActions(frame, blockSimulation);

        if (_simulation.HasPendingSimulationWork)
        {
            _simulation.Tick();
        }

        _snapshot = _simulation.BuildRenderSnapshot();
        if (_simulation.LastSnapshotRebuilt)
        {
            _debugStats.NotifySnapshotRebuild();
        }

        _renderer.UpdatePlayerAnimation(deltaSeconds, _snapshot);

        if (_snapshot.PlayerX != _lastPlayerX || _snapshot.PlayerY != _lastPlayerY)
        {
            _camera.CenterOn(_snapshot.PlayerX, _snapshot.PlayerY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            _lastPlayerX = _snapshot.PlayerX;
            _lastPlayerY = _snapshot.PlayerY;
        }

        base.Update(gameTime);

        _debugStats.UpdateMs = _frameStopwatch.Elapsed.TotalMilliseconds;
        _debugStats.EndFrame(deltaSeconds);
    }

    private void UpdateHoverTooltip(InputFrame frame)
    {
        if (_menu.IsOpen || _uiManager.ShouldBlockSimulation)
        {
            ClearHoverTooltip();
            return;
        }

        if (_uiManager.ContainsHudPoint(frame.MouseX, frame.MouseY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))
        {
            ClearHoverTooltip();
            return;
        }

        if (GridPicker.TryScreenToGrid(_camera, frame.MouseX, frame.MouseY, _snapshot.GridWidth, _snapshot.GridHeight, out int gx, out int gy))
        {
            if (gx != _hoverGridX || gy != _hoverGridY || _snapshot.ViewMode != _hoverViewMode)
            {
                _hoverGridX = gx;
                _hoverGridY = gy;
                _hoverViewMode = _snapshot.ViewMode;
                _simulation.HoverTooltip = _simulation.Session.InspectTile(gx, gy);
            }
        }
        else
        {
            ClearHoverTooltip();
        }
    }

    private void ClearHoverTooltip()
    {
        _hoverGridX = -1;
        _hoverGridY = -1;
        _simulation.HoverTooltip = null;
    }

    private void HandleMenuAndSelection(InputFrame frame, out bool menuClosedThisFrame)
    {
        menuClosedThisFrame = false;

        if (frame.Pressed.Contains(InputAction.OpenContextMenu) && !_menu.IsOpen && !_uiManager.ShouldBlockSimulation)
        {
            if (_uiManager.ContainsHudPoint(frame.MouseX, frame.MouseY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))
            {
                return;
            }

            if (GridPicker.TryScreenToGrid(_camera, frame.MouseX, frame.MouseY, _snapshot.GridWidth, _snapshot.GridHeight, out int gx, out int gy))
            {
                _selection.Lock(gx, gy, _snapshot.ViewMode);
                List<ContextMenuItem> items = ContextMenuBuilder.Build(
                    _snapshot.ViewMode,
                    gx,
                    gy,
                    _content.ContextMenus,
                    _simulation.Overworld,
                    _simulation.Session,
                    _simulation.BlueprintCatalog);

                _menu.Open(
                    frame.MouseX,
                    frame.MouseY,
                    gx,
                    gy,
                    items,
                    _renderer.MenuFont,
                    GraphicsDevice.Viewport.Width,
                    GraphicsDevice.Viewport.Height);
            }
        }

        if (frame.Pressed.Contains(InputAction.ConfirmMenu) && _menu.IsOpen)
        {
            if (_menu.TryGetEntryAt(frame.MouseX, frame.MouseY, out ContextMenuItem entry))
            {
                DispatchMenuIntent(entry);
            }

            _menu.Close();
            _selection.Clear();
        }

        if (frame.Pressed.Contains(InputAction.MoveToSelected) &&
            !frame.Pressed.Contains(InputAction.ConfirmMenu) &&
            !frame.Pressed.Contains(InputAction.OpenContextMenu) &&
            !_menu.IsOpen &&
            !_uiManager.ShouldBlockSimulation)
        {
            if (_uiManager.ContainsHudPoint(frame.MouseX, frame.MouseY, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height))
            {
                return;
            }

            if (GridPicker.TryScreenToGrid(_camera, frame.MouseX, frame.MouseY, _snapshot.GridWidth, _snapshot.GridHeight, out int gx, out int gy))
            {
                _simulation.QueueIntent(GameIntent.MoveToSelected, gx, gy);
            }
        }

        if (frame.Pressed.Contains(InputAction.CancelMenu))
        {
            if (_menu.IsOpen)
            {
                _menu.Close();
                _selection.Clear();
                menuClosedThisFrame = true;
            }
        }
    }

    private void DispatchMenuIntent(ContextMenuItem entry)
    {
        GameIntent intent = ContextMenuController.ResolveIntent(entry);
        _simulation.QueueIntent(intent, _menu.TileX, _menu.TileY);
    }

    private void HandleSimulationActions(InputFrame frame, bool blockSimulation)
    {
        if (blockSimulation)
        {
            return;
        }

        if (frame.Pressed.Contains(InputAction.MoveNorth))
        {
            _simulation.QueueIntent(GameIntent.MoveNorth);
        }

        if (frame.Pressed.Contains(InputAction.MoveSouth))
        {
            _simulation.QueueIntent(GameIntent.MoveSouth);
        }

        if (frame.Pressed.Contains(InputAction.MoveWest))
        {
            _simulation.QueueIntent(GameIntent.MoveWest);
        }

        if (frame.Pressed.Contains(InputAction.MoveEast))
        {
            _simulation.QueueIntent(GameIntent.MoveEast);
        }

        if (frame.Pressed.Contains(InputAction.MoveNorthWest))
        {
            _simulation.QueueIntent(GameIntent.MoveNorthWest);
        }

        if (frame.Pressed.Contains(InputAction.MoveNorthEast))
        {
            _simulation.QueueIntent(GameIntent.MoveNorthEast);
        }

        if (frame.Pressed.Contains(InputAction.MoveSouthWest))
        {
            _simulation.QueueIntent(GameIntent.MoveSouthWest);
        }

        if (frame.Pressed.Contains(InputAction.MoveSouthEast))
        {
            _simulation.QueueIntent(GameIntent.MoveSouthEast);
        }

        if (frame.Pressed.Contains(InputAction.EnterCell))
        {
            _simulation.QueueIntent(GameIntent.EnterCell);
        }

        if (frame.Pressed.Contains(InputAction.LeaveLocalMap))
        {
            _simulation.QueueIntent(GameIntent.LeaveLocalMap);
        }

        if (frame.Pressed.Contains(InputAction.RemoveTerrain))
        {
            _simulation.QueueIntent(GameIntent.RemoveTerrain);
        }

        if (frame.Pressed.Contains(InputAction.Wait))
        {
            _simulation.QueueIntent(GameIntent.Wait);
        }

        if (frame.Pressed.Contains(InputAction.SaveGame))
        {
            GameBootstrap.SaveGame(_simulation, _saveManager, _content);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        _frameStopwatch.Restart();

        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        _renderer.Draw(_snapshot, _camera, _selection, _menu, _uiManager, mouse.X, mouse.Y, _debugStats);

        _debugStats.DrawMs = _frameStopwatch.Elapsed.TotalMilliseconds;
        base.Draw(gameTime);
    }
}
