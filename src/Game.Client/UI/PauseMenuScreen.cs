using Game.Client.Input;
using Game.Simulation.Rendering;
using Game.Simulation.Session;

namespace Game.Client.UI;

public sealed class PauseMenuScreen : IUiScreen
{
    public const int EntryHeight = 24;
    public const int Padding = 8;
    public const int MinMenuWidth = 180;

    private readonly Action _onSave;
    private readonly Action _onQuit;
    private readonly Action _onLeaveLocalMap;

    private readonly List<PauseMenuEntry> _entries = [];
    private int _menuX;
    private int _menuY;
    private int _menuWidth;
    private int _menuHeight;

    public PauseMenuScreen(Action onSave, Action onQuit, Action onLeaveLocalMap)
    {
        _onSave = onSave;
        _onQuit = onQuit;
        _onLeaveLocalMap = onLeaveLocalMap;
    }

    public bool IsModal => true;

    public void HandleInput(InputFrame frame, UiInputResult result, RenderSnapshot snapshot, int viewportWidth, int viewportHeight)
    {
        RebuildEntries(snapshot.ViewMode);
        Layout(viewportWidth, viewportHeight);

        if (frame.Pressed.Contains(InputAction.OpenPauseMenu) ||
            frame.Pressed.Contains(InputAction.CancelMenu))
        {
            result.RequestCloseTopScreen = true;
            result.InputConsumed = true;
            return;
        }

        if (frame.Pressed.Contains(InputAction.ConfirmMenu))
        {
            if (TryGetEntryAt(frame.MouseX, frame.MouseY, out PauseMenuEntry entry))
            {
                if (entry.Action == PauseMenuAction.Resume)
                {
                    result.RequestCloseTopScreen = true;
                }
                else
                {
                    ExecuteEntry(entry);
                }

                result.InputConsumed = true;
            }
        }
    }

    public void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        RebuildEntries(snapshot.ViewMode);
        Layout(viewportWidth, viewportHeight);

        painter.DrawRect(_menuX, _menuY, _menuWidth, _menuHeight, theme.PanelBackground);
        painter.DrawBorder(_menuX, _menuY, _menuWidth, _menuHeight, theme.PanelBorder, 1);
        painter.DrawString("Paused", _menuX + Padding, _menuY + 4, theme.TextPrimary);

        int hovered = HoveredIndex(mouseX, mouseY);
        for (int i = 0; i < _entries.Count; i++)
        {
            int entryY = _menuY + Padding + 20 + i * EntryHeight;
            if (i == hovered)
            {
                painter.DrawRect(_menuX + 2, entryY + 1, _menuWidth - 4, EntryHeight - 2, theme.PanelAccent);
            }

            if (i > 0)
            {
                painter.DrawRect(_menuX + 14, entryY, _menuWidth - 28, 1, theme.PanelDivider);
            }

            painter.DrawString(_entries[i].Label, _menuX + 14, entryY + 5, theme.TextPrimary);
        }
    }

    private void RebuildEntries(GameViewMode viewMode)
    {
        _entries.Clear();
        _entries.Add(new PauseMenuEntry(PauseMenuAction.Resume, "Resume"));
        _entries.Add(new PauseMenuEntry(PauseMenuAction.Save, "Save"));
        if (viewMode == GameViewMode.LocalMap)
        {
            _entries.Add(new PauseMenuEntry(PauseMenuAction.LeaveLocalMap, "Leave Local Map"));
        }

        _entries.Add(new PauseMenuEntry(PauseMenuAction.Settings, "Settings"));
        _entries.Add(new PauseMenuEntry(PauseMenuAction.Quit, "Quit to Desktop"));
    }

    private void Layout(int viewportWidth, int viewportHeight)
    {
        _menuWidth = MinMenuWidth;
        foreach (PauseMenuEntry entry in _entries)
        {
            float width = painterSafeMeasure(entry.Label) + 28;
            _menuWidth = Math.Max(_menuWidth, (int)width);
        }

        _menuHeight = Padding * 2 + 20 + _entries.Count * EntryHeight;
        _menuX = (viewportWidth - _menuWidth) / 2;
        _menuY = (viewportHeight - _menuHeight) / 2;
    }

    private float painterSafeMeasure(string text) => text.Length * 8f;

    private bool TryGetEntryAt(int screenX, int screenY, out PauseMenuEntry entry)
    {
        entry = default!;
        if (screenX < _menuX || screenX > _menuX + _menuWidth)
        {
            return false;
        }

        int relativeY = screenY - (_menuY + Padding + 20);
        if (relativeY < 0)
        {
            return false;
        }

        int index = relativeY / EntryHeight;
        if (index < 0 || index >= _entries.Count)
        {
            return false;
        }

        entry = _entries[index];
        return true;
    }

    private int HoveredIndex(int screenX, int screenY)
    {
        return TryGetEntryAt(screenX, screenY, out _) ? (screenY - (_menuY + Padding + 20)) / EntryHeight : -1;
    }

    private void ExecuteEntry(PauseMenuEntry entry)
    {
        switch (entry.Action)
        {
            case PauseMenuAction.Save:
                _onSave();
                break;
            case PauseMenuAction.LeaveLocalMap:
                _onLeaveLocalMap();
                break;
            case PauseMenuAction.Settings:
                break;
            case PauseMenuAction.Quit:
                _onQuit();
                break;
        }
    }

    private enum PauseMenuAction
    {
        Resume,
        Save,
        LeaveLocalMap,
        Settings,
        Quit
    }

    private readonly record struct PauseMenuEntry(PauseMenuAction Action, string Label);
}
