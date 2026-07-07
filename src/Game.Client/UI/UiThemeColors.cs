using Game.Client.Presentation;
using Game.Content.Definitions;
using Microsoft.Xna.Framework;

namespace Game.Client.UI;

public sealed class UiThemeColors
{
    public Color PanelBackground { get; }
    public Color PanelBorder { get; }
    public Color PanelAccent { get; }
    public Color PanelDivider { get; }
    public Color TextPrimary { get; }
    public Color TextSecondary { get; }
    public Color TextHighlight { get; }
    public Color HpBarFill { get; }
    public Color HpBarBackground { get; }
    public Color ButtonBackground { get; }
    public Color ButtonHover { get; }
    public Color TooltipBackground { get; }

    public UiThemeColors(UiThemeDefinition theme)
    {
        PanelBackground = ColorParser.Parse(theme.PanelBackground, new Color(0x16, 0x21, 0x3E));
        PanelBorder = ColorParser.Parse(theme.PanelBorder, new Color(0xE9, 0x45, 0x60));
        PanelAccent = ColorParser.Parse(theme.PanelAccent, new Color(0x0F, 0x34, 0x60));
        PanelDivider = ColorParser.Parse(theme.PanelDivider, new Color(0x2A, 0x3A, 0x5C));
        TextPrimary = ColorParser.Parse(theme.TextPrimary, Color.White);
        TextSecondary = ColorParser.Parse(theme.TextSecondary, Color.LightGray);
        TextHighlight = ColorParser.Parse(theme.TextHighlight, Color.Cyan);
        HpBarFill = ColorParser.Parse(theme.HpBarFill, new Color(0xE9, 0x45, 0x60));
        HpBarBackground = ColorParser.Parse(theme.HpBarBackground, new Color(0x2A, 0x3A, 0x5C));
        ButtonBackground = ColorParser.Parse(theme.ButtonBackground, new Color(0x0F, 0x34, 0x60));
        ButtonHover = ColorParser.Parse(theme.ButtonHover, new Color(0x1A, 0x4A, 0x7A));
        TooltipBackground = ColorParser.Parse(theme.TooltipBackground, new Color(0x16, 0x21, 0x3E));
    }
}
