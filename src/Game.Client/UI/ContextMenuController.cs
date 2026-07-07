using Game.Simulation.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.UI;

public sealed class ContextMenuController
{
    public bool IsOpen { get; private set; }
    public int ScreenX { get; private set; }
    public int ScreenY { get; private set; }
    public int TileX { get; private set; }
    public int TileY { get; private set; }
    public IReadOnlyList<ContextMenuItem> Entries { get; private set; } = [];

    public int MenuWidth { get; private set; } = 240;
    public int MenuHeight { get; private set; }

    public const int EntryHeight = 24;
    public const int Padding = 6;
    public const int TextPaddingX = 10;
    public const int TextPaddingY = 5;
    public const int MinMenuWidth = 220;
    public const int MaxMenuWidth = 360;

    public void Open(
        int screenX,
        int screenY,
        int tileX,
        int tileY,
        IReadOnlyList<ContextMenuItem> entries,
        SpriteFont? font,
        int viewportWidth,
        int viewportHeight)
    {
        Entries = entries;
        TileX = tileX;
        TileY = tileY;
        Layout(entries, font, screenX, screenY, viewportWidth, viewportHeight);
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    public bool TryGetEntryAt(int screenX, int screenY, out ContextMenuItem entry)
    {
        entry = null!;
        if (!IsOpen)
        {
            return false;
        }

        if (screenX < ScreenX || screenX > ScreenX + MenuWidth)
        {
            return false;
        }

        int relativeY = screenY - (ScreenY + Padding);
        if (relativeY < 0)
        {
            return false;
        }

        int index = relativeY / EntryHeight;
        if (index < 0 || index >= Entries.Count)
        {
            return false;
        }

        entry = Entries[index];
        return true;
    }

    public int HoveredIndex(int screenX, int screenY)
    {
        if (TryGetEntryAt(screenX, screenY, out ContextMenuItem entry))
        {
            return Entries.ToList().IndexOf(entry);
        }

        return -1;
    }

    public static GameIntent ResolveIntent(ContextMenuItem entry)
    {
        return Enum.Parse<GameIntent>(entry.Intent);
    }

    private void Layout(
        IReadOnlyList<ContextMenuItem> entries,
        SpriteFont? font,
        int screenX,
        int screenY,
        int viewportWidth,
        int viewportHeight)
    {
        MenuWidth = MinMenuWidth;
        if (font is not null)
        {
            foreach (ContextMenuItem entry in entries)
            {
                Vector2 size = font.MeasureString(entry.Label);
                int requiredWidth = (int)Math.Ceiling(size.X) + TextPaddingX * 2;
                MenuWidth = Math.Max(MenuWidth, requiredWidth);
            }
        }

        MenuWidth = Math.Clamp(MenuWidth, MinMenuWidth, MaxMenuWidth);
        MenuHeight = Padding * 2 + entries.Count * EntryHeight;

        ScreenX = screenX;
        ScreenY = screenY;

        if (ScreenX + MenuWidth > viewportWidth)
        {
            ScreenX = Math.Max(0, viewportWidth - MenuWidth);
        }

        if (ScreenY + MenuHeight > viewportHeight)
        {
            ScreenY = Math.Max(0, viewportHeight - MenuHeight);
        }
    }
}
