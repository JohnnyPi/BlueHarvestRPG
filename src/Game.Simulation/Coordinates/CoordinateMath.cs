using Game.Simulation.LocalMaps;

namespace Game.Simulation.Coordinates;

public static class CoordinateMath
{
    public static GlobalTileCoord ToGlobalTile(WorldCoord world, LocalCoord local)
    {
        return new GlobalTileCoord(
            world.X * LocalMap.Width + local.X,
            world.Y * LocalMap.Height + local.Y);
    }

    public static (WorldCoord World, LocalCoord Local) FromGlobalTile(GlobalTileCoord global)
    {
        int localMapSize = LocalMap.Width;
        int worldX = Math.DivRem(global.X, localMapSize, out int localX);
        int worldY = Math.DivRem(global.Y, localMapSize, out int localY);

        if (localX < 0)
        {
            worldX--;
            localX += localMapSize;
        }

        if (localY < 0)
        {
            worldY--;
            localY += localMapSize;
        }

        return (new WorldCoord(worldX, worldY), new LocalCoord(localX, localY));
    }

    public static bool OverlapsCell(
        int globalOriginX,
        int globalOriginY,
        int width,
        int height,
        WorldCoord cell)
    {
        int cellMinX = cell.X * LocalMap.Width;
        int cellMinY = cell.Y * LocalMap.Height;
        int cellMaxX = cellMinX + LocalMap.Width - 1;
        int cellMaxY = cellMinY + LocalMap.Height - 1;

        int rectMaxX = globalOriginX + width - 1;
        int rectMaxY = globalOriginY + height - 1;

        return globalOriginX <= cellMaxX &&
               rectMaxX >= cellMinX &&
               globalOriginY <= cellMaxY &&
               rectMaxY >= cellMinY;
    }
}
