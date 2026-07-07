using Game.Client.Presentation.Camera;
using Microsoft.Xna.Framework;

namespace Game.Client.Presentation;

public static class GridPicker
{
    public static bool TryScreenToGrid(
        CameraController camera,
        int screenX,
        int screenY,
        int gridWidth,
        int gridHeight,
        out int gridX,
        out int gridY)
    {
        Vector2 world = camera.ScreenToWorld(new Vector2(screenX, screenY));
        gridX = (int)Math.Floor(world.X);
        gridY = (int)Math.Floor(world.Y);

        return gridX >= 0 && gridY >= 0 && gridX < gridWidth && gridY < gridHeight;
    }
}
