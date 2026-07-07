using Game.Client.Input;
using Game.Simulation.Quests;
using Game.Simulation.Rendering;
using Game.Simulation.Session;

namespace Game.Client.UI;

public sealed class CharacterSheetScreen : IUiScreen
{
    private const int PanelWidth = 420;
    private const int PanelHeight = 480;
    private const int Padding = 12;
    private const int LineHeight = 16;

    public bool IsModal => true;

    public void HandleInput(InputFrame frame, UiInputResult result, RenderSnapshot snapshot, int viewportWidth, int viewportHeight)
    {
        if (frame.Pressed.Contains(InputAction.OpenCharacterSheet) ||
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
        painter.DrawString("Character", panelX + Padding, panelY + Padding, theme.TextPrimary);

        int y = panelY + Padding + 28;
        PlayerStatusView status = snapshot.PlayerStatus;
        CharacterSheetView sheet = snapshot.CharacterSheet;

        DrawLine(painter, theme, panelX, ref y, $"Level {sheet.Level}  XP {sheet.Experience}");
        DrawLine(painter, theme, panelX, ref y, $"HP {status.Health}/{status.MaxHealth}");
        if (snapshot.ViewMode == GameViewMode.Overworld)
        {
            DrawLine(painter, theme, panelX, ref y, $"Stamina {status.Energy}  Speed {status.Speed}");
        }
        else
        {
            DrawLine(painter, theme, panelX, ref y, $"Speed {status.Speed}");
        }
        DrawLine(painter, theme, panelX, ref y, $"Faction {sheet.Faction}");
        DrawLine(painter, theme, panelX, ref y, $"World ({status.WorldX}, {status.WorldY})");

        if (status.LocalX.HasValue && status.LocalY.HasValue)
        {
            DrawLine(painter, theme, panelX, ref y, $"Local ({status.LocalX}, {status.LocalY})");
        }

        y += 8;
        painter.DrawString("Attributes", panelX + Padding, y, theme.TextPrimary);
        y += LineHeight + 4;

        foreach (AttributeView attribute in sheet.Attributes)
        {
            DrawLine(painter, theme, panelX, ref y, $"{attribute.DisplayName}: {attribute.Value}");
        }

        y += 8;
        painter.DrawString("Inventory", panelX + Padding, y, theme.TextPrimary);
        y += LineHeight + 4;
        DrawLine(painter, theme, panelX, ref y, $"{sheet.InventoryStackCount} stacks, {sheet.InventoryTotalCount} items");

        y += 8;
        painter.DrawString("Quests", panelX + Padding, y, theme.TextPrimary);
        y += LineHeight + 4;

        foreach (QuestItemView quest in snapshot.QuestItems)
        {
            string state = quest.State == QuestState.Completed ? " [done]" : string.Empty;
            DrawLine(painter, theme, panelX, ref y, $"{quest.Title}: {quest.Progress}/{quest.Target}{state}");
        }
    }

    private static void DrawLine(UiPainter painter, UiThemeColors theme, int panelX, ref int y, string text)
    {
        painter.DrawString(text, panelX + Padding, y, theme.TextSecondary);
        y += LineHeight;
    }
}
