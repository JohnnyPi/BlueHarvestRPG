using Game.Client.Input;
using Game.Simulation.Rendering;

namespace Game.Client.UI;

public sealed class InventoryScreen : IUiScreen
{
    private const int PanelWidth = 360;
    private const int PanelHeight = 420;
    private const int Padding = 12;
    private const int RowHeight = 18;

    public bool IsModal => true;

    public void HandleInput(InputFrame frame, UiInputResult result, RenderSnapshot snapshot, int viewportWidth, int viewportHeight)
    {
        if (frame.Pressed.Contains(InputAction.OpenInventory) ||
            frame.Pressed.Contains(InputAction.OpenPauseMenu) ||
            frame.Pressed.Contains(InputAction.CancelMenu))
        {
            result.RequestCloseTopScreen = true;
            result.InputConsumed = true;
        }
    }

    public void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        int panelX = (viewportWidth - PanelWidth) / 2;
        int panelY = (viewportHeight - PanelHeight) / 2;

        painter.DrawRect(panelX, panelY, PanelWidth, PanelHeight, theme.PanelBackground);
        painter.DrawBorder(panelX, panelY, PanelWidth, PanelHeight, theme.PanelBorder, 1);
        painter.DrawString("Inventory", panelX + Padding, panelY + Padding, theme.TextPrimary);

        int y = panelY + Padding + 28;
        if (snapshot.InventoryItems.Length == 0)
        {
            painter.DrawString("Empty", panelX + Padding, y, theme.TextSecondary);
            return;
        }

        foreach (InventoryItemView item in snapshot.InventoryItems)
        {
            painter.DrawString($"{item.DisplayName} x{item.Count}", panelX + Padding, y, theme.TextSecondary);
            y += RowHeight;
            if (y > panelY + PanelHeight - Padding)
            {
                break;
            }
        }
    }
}
