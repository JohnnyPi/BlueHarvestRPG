using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

internal static class StructureStampHelper
{
    public static void StampRect(
        LocalMap map,
        WorldCoord worldCell,
        int globalOriginX,
        int globalOriginY,
        int width,
        int height,
        Action<LocalMap, int, int, int, int> stampTile)
    {
        int cellMinX = worldCell.X * LocalMap.Width;
        int cellMinY = worldCell.Y * LocalMap.Height;

        for (int gy = globalOriginY; gy < globalOriginY + height; gy++)
        {
            for (int gx = globalOriginX; gx < globalOriginX + width; gx++)
            {
                if (gx < cellMinX || gx >= cellMinX + LocalMap.Width ||
                    gy < cellMinY || gy >= cellMinY + LocalMap.Height)
                {
                    continue;
                }

                int localX = gx - cellMinX;
                int localY = gy - cellMinY;
                int withinX = gx - globalOriginX;
                int withinY = gy - globalOriginY;
                stampTile(map, localX, localY, withinX, withinY);
            }
        }
    }

    public static void StampBuilding(LocalMap map, WorldCoord worldCell, StructurePlacement structure)
    {
        StampRect(
            map,
            worldCell,
            structure.GlobalOriginX,
            structure.GlobalOriginY,
            structure.Width,
            structure.Height,
            (m, localX, localY, withinX, withinY) =>
            {
                bool isPerimeter =
                    withinX == 0 || withinY == 0 ||
                    withinX == structure.Width - 1 || withinY == structure.Height - 1;

                if (!isPerimeter)
                {
                    TerrainId floor = structure.Type is StructureType.Helipad or StructureType.Dock
                        ? TerrainId.Concrete
                        : TerrainId.Floor;
                    m.SetTerrain(localX, localY, floor, TileFlags.None);
                    return;
                }

                if (structure.Type == StructureType.Dock && withinY == structure.Height - 1)
                {
                    m.SetTerrain(localX, localY, TerrainId.Dock, TileFlags.None);
                    return;
                }

                if (structure.Type != StructureType.Helipad &&
                    withinX == structure.Width / 2 &&
                    withinY == structure.Height - 1)
                {
                    m.SetTerrain(localX, localY, TerrainId.Door, TileFlags.None);
                    return;
                }

                m.SetTerrain(localX, localY, TerrainId.Wall, TileFlags.BlocksMovement | TileFlags.BlocksVision);
            });
    }
}
