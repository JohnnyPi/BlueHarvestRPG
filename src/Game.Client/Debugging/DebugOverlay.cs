using Game.Simulation.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Debugging;

public sealed class DebugOverlay
{
    private const double SlowFrameThresholdMs = 20.0;

    public void Draw(
        SpriteBatch spriteBatch,
        SpriteFont font,
        DebugFrameStats stats,
        RenderSnapshot snapshot,
        int viewportWidth,
        int viewportHeight)
    {
        if (!DebugMode.IsEnabled)
        {
            return;
        }

        if (stats.UpdateMs > SlowFrameThresholdMs || stats.DrawMs > SlowFrameThresholdMs)
        {
            DebugLog.Issue(
                $"Slow frame: update={stats.UpdateMs:F1}ms draw={stats.DrawMs:F1}ms visibleCells={stats.VisibleCells}");
        }

        if (stats.SnapshotRebuildsThisSecond > 30)
        {
            DebugLog.Issue($"High snapshot rebuild rate: {stats.SnapshotRebuildsThisSecond}/s");
        }

        var lines = new List<string>
        {
            $"DEBUG (F3)  FPS {stats.Fps:F0}",
            $"Update {stats.UpdateMs:F1} ms  Draw {stats.DrawMs:F1} ms",
            $"Visible cells {stats.VisibleCells}  Draw rects {stats.DrawRects}",
            $"Snapshot rebuilds/s {stats.SnapshotRebuildsThisSecond}",
            $"GC gen0/1/2 {stats.GcGen0}/{stats.GcGen1}/{stats.GcGen2}  alloc {stats.AllocatedBytes / 1024} KB",
        };

        if (snapshot.ViewMode == Game.Simulation.Session.GameViewMode.Overworld)
        {
            lines.Add("F4: reveal map");
        }

        lines.Add("---");

        if (!string.IsNullOrEmpty(snapshot.DebugInfo))
        {
            lines.AddRange(snapshot.DebugInfo.Split('\n'));
        }

        int panelWidth = 360;
        int panelHeight = 16 + lines.Count * 14;
        int x = 8;
        int y = viewportHeight - panelHeight - 8;

        DrawPanel(spriteBatch, font, x, y, panelWidth, panelHeight, lines);
    }

    private static void DrawPanel(
        SpriteBatch spriteBatch,
        SpriteFont font,
        int x,
        int y,
        int width,
        int height,
        IReadOnlyList<string> lines)
    {
        Texture2D? pixel = GetPixel(spriteBatch.GraphicsDevice);
        if (pixel is not null)
        {
            spriteBatch.Draw(pixel, new Rectangle(x, y, width, height), new Color(0, 0, 0, 190));
        }

        int lineY = y + 6;
        foreach (string line in lines)
        {
            spriteBatch.DrawString(font, line, new Vector2(x + 8, lineY), Color.Yellow);
            lineY += 14;
        }
    }

    private static Texture2D? _pixel;

    private static Texture2D? GetPixel(GraphicsDevice device)
    {
        if (_pixel is not null)
        {
            return _pixel;
        }

        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData([Color.White]);
        return _pixel;
    }
}
