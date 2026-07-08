using Game.Client.Presentation.Camera;
using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

public static class GridPicker
{
    public static bool TryGetTileScreenBounds(CameraController camera, int gridX, int gridY, out Rectangle bounds)
    {
        int tileSize = Math.Max(1, (int)Math.Round(camera.CellSize));
        Vector2 screen = camera.WorldToScreen(gridX, gridY);
        int sx = (int)Math.Round(screen.X);
        int sy = (int)Math.Round(screen.Y);
        bounds = new Rectangle(sx, sy, tileSize, tileSize);
        return true;
    }

    public static bool TryScreenToGrid(
        CameraController camera,
        int screenX,
        int screenY,
        int gridWidth,
        int gridHeight,
        out int gridX,
        out int gridY)
    {
        int tileSize = Math.Max(1, (int)Math.Round(camera.CellSize));
        Vector2 world = camera.ScreenToWorld(new Vector2(screenX, screenY));
        int guessX = (int)MathF.Floor(world.X);
        int guessY = (int)MathF.Floor(world.Y);

        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int x = guessX + dx;
                int y = guessY + dy;
                if (x < 0 || y < 0 || x >= gridWidth || y >= gridHeight)
                {
                    continue;
                }

                Vector2 screen = camera.WorldToScreen(x, y);
                int sx = (int)Math.Round(screen.X);
                int sy = (int)Math.Round(screen.Y);
                if (new Rectangle(sx, sy, tileSize, tileSize).Contains(screenX, screenY))
                {
                    gridX = x;
                    gridY = y;
                    return true;
                }
            }
        }

        gridX = guessX;
        gridY = guessY;
        return gridX >= 0 && gridY >= 0 && gridX < gridWidth && gridY < gridHeight;
    }
}
