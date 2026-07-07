using Game.Client.Input;
using Game.Content;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Input;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class UiManager
{
    private readonly UiScreenStack _stack = new();
    private readonly SideHudPanel _sideHud = new();
    private readonly UiThemeColors _theme;
    private readonly PauseMenuScreen _pauseMenu;
    private readonly InventoryScreen _inventoryScreen = new();
    private readonly CharacterSheetScreen _characterSheetScreen = new();
    private readonly RunEndScreen _runEndScreen;
    private readonly SimulationHost _simulation;
    private readonly SaveManager _saveManager;
    private readonly GameContentBundle _content;
    private readonly Action _requestExit;

    private readonly UiInputResult _inputResult = new();
    private bool _contextMenuOpen;
    private bool _runEndShown;

    public UiManager(
        GameContentBundle content,
        SaveManager saveManager,
        SimulationHost simulation,
        Action requestExit)
    {
        _content = content;
        _saveManager = saveManager;
        _simulation = simulation;
        _requestExit = requestExit;
        _theme = new UiThemeColors(content.UiTheme);
        _pauseMenu = new PauseMenuScreen(
            onSave: () => GameBootstrap.SaveGame(simulation, saveManager, content),
            onQuit: () =>
            {
                GameBootstrap.SaveGame(simulation, saveManager, content);
                requestExit();
            },
            onLeaveLocalMap: () => simulation.QueueIntent(GameIntent.LeaveLocalMap));
        _runEndScreen = new RunEndScreen(() =>
        {
            GameBootstrap.SaveGame(simulation, saveManager, content);
            requestExit();
        });
    }

    public bool ShouldBlockSimulation => _contextMenuOpen || _stack.BlocksSimulationInput;

    public bool ShouldBlockWorldClick => _sideHud.ContainsPoint(0, 0, 1280, 720) || _stack.HasModalScreen;

    public void SetContextMenuOpen(bool isOpen) => _contextMenuOpen = isOpen;

    public bool ContainsHudPoint(int x, int y, int viewportWidth, int viewportHeight)
    {
        return _sideHud.ContainsPoint(x, y, viewportWidth, viewportHeight);
    }

    public void HandleInput(InputFrame frame, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, bool contextMenuOpen, bool suppressPauseToggle = false)
    {
        _contextMenuOpen = contextMenuOpen;
        _inputResult.Reset();

        if (snapshot.RunOutcome != RunOutcome.None && !_runEndShown)
        {
            _stack.Push(_runEndScreen);
            _runEndShown = true;
        }

        if (contextMenuOpen)
        {
            return;
        }

        if (frame.Pressed.Contains(InputAction.ConfirmMenu) || frame.Pressed.Contains(InputAction.OpenContextMenu))
        {
            if (_sideHud.TryHandleClick(frame.MouseX, frame.MouseY, viewportWidth, viewportHeight, _inputResult))
            {
                ApplyInputRequests(frame, suppressPauseToggle);
                return;
            }
        }

        if (_stack.Top is not null)
        {
            _stack.Top.HandleInput(frame, _inputResult, snapshot, viewportWidth, viewportHeight);
            ApplyInputRequests(frame, suppressPauseToggle);
            return;
        }

        if (frame.Pressed.Contains(InputAction.OpenPauseMenu) && !suppressPauseToggle)
        {
            _inputResult.RequestOpenPauseMenu = true;
        }

        if (frame.Pressed.Contains(InputAction.OpenInventory))
        {
            _inputResult.RequestToggleInventory = true;
        }

        if (frame.Pressed.Contains(InputAction.OpenCharacterSheet))
        {
            _inputResult.RequestToggleCharacterSheet = true;
        }

        if (frame.Pressed.Contains(InputAction.ConfirmMenu))
        {
            _sideHud.TryHandleClick(frame.MouseX, frame.MouseY, viewportWidth, viewportHeight, _inputResult);
        }

        ApplyInputRequests(frame, suppressPauseToggle);
    }

    private void ApplyInputRequests(InputFrame frame, bool suppressPauseToggle)
    {
        if (_inputResult.RequestCloseTopScreen)
        {
            _stack.Pop();
        }
        else if (_inputResult.RequestOpenPauseMenu && !suppressPauseToggle)
        {
            ToggleScreen<PauseMenuScreen>(_pauseMenu);
        }

        if (_inputResult.RequestOpenPauseFromHud)
        {
            if (!_stack.Contains<PauseMenuScreen>())
            {
                _stack.Push(_pauseMenu);
            }
        }

        if (_inputResult.RequestToggleInventory)
        {
            ToggleScreen<InventoryScreen>(_inventoryScreen);
        }

        if (_inputResult.RequestToggleCharacterSheet)
        {
            ToggleScreen<CharacterSheetScreen>(_characterSheetScreen);
        }
    }

    private void ToggleScreen<T>(T screen) where T : IUiScreen
    {
        if (_stack.Top is T)
        {
            _stack.Pop();
            return;
        }

        _stack.Push(screen);
    }

    public void Draw(UiPainter painter, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        _sideHud.Draw(painter, _theme, snapshot, viewportWidth, viewportHeight, mouseX, mouseY);
        _stack.Draw(painter, _theme, snapshot, viewportWidth, viewportHeight, mouseX, mouseY);

        if (!string.IsNullOrEmpty(snapshot.HoverTooltip))
        {
            DrawTooltip(painter, snapshot.HoverTooltip!, mouseX, mouseY, viewportWidth, viewportHeight);
        }
    }

    private void DrawTooltip(UiPainter painter, string text, int mouseX, int mouseY, int viewportWidth, int viewportHeight)
    {
        Vector2 size = painter.MeasureString(text);
        int width = Math.Max(80, (int)size.X + 12);
        int height = Math.Max(20, (int)size.Y + 8);
        int x = Math.Min(mouseX + 12, viewportWidth - width - 4);
        int y = Math.Min(mouseY + 12, viewportHeight - height - 4);

        painter.DrawRect(x, y, width, height, _theme.TooltipBackground with { A = 230 });
        painter.DrawBorder(x, y, width, height, _theme.PanelBorder, 1);
        painter.DrawString(text, x + 6, y + 4, _theme.TextPrimary);
    }
}
