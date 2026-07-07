using Game.Client.Input;
using Game.Client.Presentation;
using Game.Simulation.Quests;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class SideHudPanel
{
    public const int PanelWidth = 220;
    private const int Padding = 10;
    private const int ButtonHeight = 22;
    private const int ButtonSpacing = 4;
    private const int LineHeight = 15;

    private Rectangle _inventoryButton;
    private Rectangle _characterButton;
    private Rectangle _menuButton;

    public SideHudPanel()
    {
    }

    public bool ContainsPoint(int x, int y, int viewportWidth, int viewportHeight)
    {
        return x >= viewportWidth - PanelWidth && y >= 0 && y < viewportHeight;
    }

    public bool TryHandleClick(int mouseX, int mouseY, int viewportWidth, int viewportHeight, UiInputResult result)
    {
        if (!ContainsPoint(mouseX, mouseY, viewportWidth, viewportHeight))
        {
            return false;
        }

        result.InputConsumed = true;
        result.BlockWorldClick = true;

        if (_inventoryButton.Contains(mouseX, mouseY))
        {
            result.RequestToggleInventory = true;
            return true;
        }

        if (_characterButton.Contains(mouseX, mouseY))
        {
            result.RequestToggleCharacterSheet = true;
            return true;
        }

        if (_menuButton.Contains(mouseX, mouseY))
        {
            result.RequestOpenPauseFromHud = true;
            return true;
        }

        return true;
    }

    public void Draw(UiPainter painter, UiThemeColors theme, RenderSnapshot snapshot, int viewportWidth, int viewportHeight, int mouseX, int mouseY)
    {
        int panelX = viewportWidth - PanelWidth;
        painter.DrawRect(panelX, 0, PanelWidth, viewportHeight, theme.PanelBackground with { A = 230 });
        painter.DrawBorder(panelX, 0, PanelWidth, viewportHeight, theme.PanelBorder, 1);

        int y = Padding;
        painter.DrawString("Status", panelX + Padding, y, theme.TextPrimary);
        y += LineHeight + 4;

        PlayerStatusView status = snapshot.PlayerStatus;
        painter.DrawString($"HP {status.Health}/{status.MaxHealth}", panelX + Padding, y, theme.TextSecondary);
        y += LineHeight;
        painter.DrawHpBar(panelX + Padding, y, PanelWidth - Padding * 2, 8, status.Health, Math.Max(1, status.MaxHealth), theme.HpBarFill, theme.HpBarBackground);
        y += 14;

        if (snapshot.ViewMode == GameViewMode.Overworld)
        {
            painter.DrawString($"Stamina {status.Energy}", panelX + Padding, y, theme.TextSecondary);
            y += LineHeight;
        }

        painter.DrawString($"Speed {status.Speed}", panelX + Padding, y, theme.TextSecondary);
        y += LineHeight + 2;

        string position = status.LocalX.HasValue && status.LocalY.HasValue
            ? $"W ({status.WorldX},{status.WorldY}) L ({status.LocalX},{status.LocalY})"
            : $"W ({status.WorldX},{status.WorldY})";
        painter.DrawString(position, panelX + Padding, y, theme.TextSecondary);
        y += LineHeight;
        painter.DrawString($"{status.TerrainOrBiome}", panelX + Padding, y, theme.TextSecondary);
        y += LineHeight;

        if (!string.IsNullOrEmpty(snapshot.ScenarioMission))
        {
            y += 4;
            painter.DrawString("Scenario", panelX + Padding, y, theme.TextPrimary);
            y += LineHeight;
            painter.DrawString(snapshot.ScenarioMission, panelX + Padding, y, theme.TextHighlight);
            y += LineHeight;
            painter.DrawString($"Pressure {snapshot.IslandPressure}/100", panelX + Padding, y, theme.TextSecondary);
            y += LineHeight;
            if (snapshot.TravelStaminaPenalty > 0)
            {
                painter.DrawString($"Storm toll +{snapshot.TravelStaminaPenalty} stamina", panelX + Padding, y, theme.TextSecondary);
                y += LineHeight;
            }

            if (snapshot.EvacHoursRemaining is int evacHours)
            {
                painter.DrawString($"Evac closes in {evacHours}h", panelX + Padding, y, theme.TextHighlight);
                y += LineHeight;
            }
        }

        if (snapshot.ViewMode == GameViewMode.Overworld && snapshot.TectonicBoundaries is not null)
        {
            y += 6;
            painter.DrawString("Geology", panelX + Padding, y, theme.TextPrimary);
            y += LineHeight + 2;
            y = DrawGeologyLegendEntry(painter, panelX + Padding, y, OverworldGeologyColors.Divergent, "Divergent rift");
            y = DrawGeologyLegendEntry(painter, panelX + Padding, y, OverworldGeologyColors.Subduction, "Subduction zone");
            y = DrawGeologyLegendEntry(painter, panelX + Padding, y, OverworldGeologyColors.Collision, "Collision front");
            y = DrawGeologyLegendEntry(painter, panelX + Padding, y, OverworldGeologyColors.Transform, "Transform fault");
            y = DrawGeologyLegendEntry(
                painter,
                panelX + Padding,
                y,
                OverworldGeologyColors.River,
                $"River ford (+{Game.Simulation.World.OverworldTravelCost.RiverCrossingCost} stamina)");
        }

        y += 6;

        painter.DrawString("Quests", panelX + Padding, y, theme.TextPrimary);
        y += LineHeight + 2;

        int questLines = 0;
        foreach (QuestItemView quest in snapshot.QuestItems)
        {
            if (quest.State != QuestState.Active || questLines >= 3)
            {
                continue;
            }

            string line = quest.Target > 1
                ? $"{quest.Title}: {quest.Progress}/{quest.Target}"
                : quest.Title;
            painter.DrawString(line, panelX + Padding, y, theme.TextHighlight);
            y += LineHeight;
            painter.DrawString(quest.Objective, panelX + Padding, y, theme.TextSecondary);
            y += LineHeight;
            questLines++;
        }

        if (questLines == 0)
        {
            painter.DrawString("None active", panelX + Padding, y, theme.TextSecondary);
            y += LineHeight;
        }

        y += 8;
        int buttonWidth = PanelWidth - Padding * 2;
        int buttonX = panelX + Padding;

        DrawButton(painter, theme, "Inventory (I)", buttonX, y, buttonWidth, ButtonHeight, mouseX, mouseY, ref _inventoryButton);
        y += ButtonHeight + ButtonSpacing;
        DrawButton(painter, theme, "Character (U)", buttonX, y, buttonWidth, ButtonHeight, mouseX, mouseY, ref _characterButton);
        y += ButtonHeight + ButtonSpacing;
        DrawButton(painter, theme, "Menu (Esc)", buttonX, y, buttonWidth, ButtonHeight, mouseX, mouseY, ref _menuButton);
    }

    private static int DrawGeologyLegendEntry(UiPainter painter, int x, int y, Color swatchColor, string label)
    {
        painter.DrawRect(x, y + 2, 10, 10, swatchColor);
        painter.DrawString(label, x + 14, y, Color.LightGray);
        return y + LineHeight;
    }

    private static void DrawButton(
        UiPainter painter,
        UiThemeColors theme,
        string label,
        int x,
        int y,
        int width,
        int height,
        int mouseX,
        int mouseY,
        ref Rectangle bounds)
    {
        bounds = new Rectangle(x, y, width, height);
        bool hovered = bounds.Contains(mouseX, mouseY);
        painter.DrawRect(x, y, width, height, hovered ? theme.ButtonHover : theme.ButtonBackground);
        painter.DrawBorder(x, y, width, height, theme.PanelDivider, 1);

        Vector2 size = painter.MeasureString(label);
        int textX = x + (int)((width - size.X) / 2);
        int textY = y + (int)((height - size.Y) / 2);
        painter.DrawString(label, textX, textY, theme.TextPrimary);
    }
}
