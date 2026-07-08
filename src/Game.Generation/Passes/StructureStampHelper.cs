using Game.Simulation.World.Island;
using Game.Content.Definitions;
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

    public static void StampBuilding(
        LocalMap map,
        WorldCoord worldCell,
        StructurePlacement structure,
        StructureBlueprintDefinition blueprint,
        int floorIndex = 0,
        bool surfaceView = true)
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
                StampStructureTile(
                    m,
                    localX,
                    localY,
                    withinX,
                    withinY,
                    structure,
                    blueprint,
                    floorIndex,
                    surfaceView);
            });

        if (surfaceView)
        {
            StampDetailFeatures(map, worldCell, structure, blueprint, floorIndex, surfaceView: true);
        }
    }

    public static void StampStructureTile(
        LocalMap map,
        int localX,
        int localY,
        int withinX,
        int withinY,
        StructurePlacement structure,
        StructureBlueprintDefinition blueprint,
        int floorIndex,
        bool surfaceView)
    {
        if (withinX == blueprint.StairX && withinY == blueprint.StairY)
        {
            StampStairs(map, localX, localY, structure, floorIndex);
            return;
        }

        if (!surfaceView &&
            blueprint.RopeExitFloor == floorIndex &&
            blueprint.RopeExitX == withinX &&
            blueprint.RopeExitY == withinY)
        {
            map.SetTerrain(localX, localY, TerrainId.Window, TileFlags.None);
            return;
        }

        bool isPerimeter =
            withinX == 0 || withinY == 0 ||
            withinX == structure.Width - 1 || withinY == structure.Height - 1;

        if (structure.Type is StructureType.Helipad or StructureType.Dock)
        {
            StampSimpleFacility(map, localX, localY, withinX, withinY, structure, blueprint, isPerimeter, surfaceView);
            return;
        }

        if (!isPerimeter)
        {
            if (IsHallwayTile(withinX, withinY, structure))
            {
                map.SetTerrain(localX, localY, TerrainId.Floor, TileFlags.None);
                return;
            }

            if (IsRoomTile(withinX, withinY, structure))
            {
                map.SetTerrain(localX, localY, TerrainId.Floor, TileFlags.None);
                return;
            }

            map.SetTerrain(localX, localY, TerrainId.InteriorWall, TileFlags.BlocksMovement | TileFlags.BlocksVision);
            return;
        }

        if (surfaceView && withinX == blueprint.DoorX && withinY == blueprint.DoorY)
        {
            map.SetTerrain(localX, localY, TerrainId.Door, TileFlags.None);
            return;
        }

        if (!surfaceView && floorIndex == 0 && withinX == blueprint.DoorX && withinY == blueprint.DoorY)
        {
            map.SetTerrain(localX, localY, TerrainId.StructureExit, TileFlags.None);
            return;
        }

        if (surfaceView && floorIndex == 0 && HasUpperFloors(structure) &&
            withinX == blueprint.StairX && withinY == blueprint.StairY)
        {
            map.SetTerrain(localX, localY, TerrainId.StairsUp, TileFlags.None);
            return;
        }

        map.SetTerrain(localX, localY, TerrainId.Wall, TileFlags.BlocksMovement | TileFlags.BlocksVision);
    }

    private static void StampSimpleFacility(
        LocalMap map,
        int localX,
        int localY,
        int withinX,
        int withinY,
        StructurePlacement structure,
        StructureBlueprintDefinition blueprint,
        bool isPerimeter,
        bool surfaceView)
    {
        if (!isPerimeter)
        {
            TerrainId floor = structure.Type == StructureType.Helipad ? TerrainId.Concrete : TerrainId.Concrete;
            map.SetTerrain(localX, localY, floor, TileFlags.None);
            return;
        }

        if (structure.Type == StructureType.Dock && withinY == structure.Height - 1)
        {
            map.SetTerrain(localX, localY, TerrainId.Dock, TileFlags.None);
            return;
        }

        if (structure.Type != StructureType.Helipad &&
            surfaceView &&
            withinX == blueprint.DoorX &&
            withinY == blueprint.DoorY)
        {
            map.SetTerrain(localX, localY, TerrainId.Door, TileFlags.None);
            return;
        }

        if (!surfaceView &&
            structure.Type != StructureType.Helipad &&
            withinX == blueprint.DoorX &&
            withinY == blueprint.DoorY)
        {
            map.SetTerrain(localX, localY, TerrainId.StructureExit, TileFlags.None);
            return;
        }

        if (structure.Type == StructureType.Helipad)
        {
            return;
        }

        map.SetTerrain(localX, localY, TerrainId.Wall, TileFlags.BlocksMovement | TileFlags.BlocksVision);
    }

    private static void StampStairs(LocalMap map, int localX, int localY, StructurePlacement structure, int floorIndex)
    {
        bool canGoUp = floorIndex < structure.MaxFloorIndex;
        bool canGoDown = floorIndex > structure.MinFloorIndex;

        if (canGoUp && canGoDown)
        {
            map.SetTerrain(localX, localY, TerrainId.StairsUp, TileFlags.None);
            return;
        }

        if (canGoUp)
        {
            map.SetTerrain(localX, localY, TerrainId.StairsUp, TileFlags.None);
            return;
        }

        if (canGoDown)
        {
            map.SetTerrain(localX, localY, TerrainId.StairsDown, TileFlags.None);
        }
    }

    private static bool HasUpperFloors(StructurePlacement structure) => structure.MaxFloorIndex > 0;

    private static bool IsHallwayTile(int withinX, int withinY, StructurePlacement structure)
    {
        int hallY = structure.Height / 2;
        return withinY == hallY && withinX > 0 && withinX < structure.Width - 1;
    }

    private static bool IsRoomTile(int withinX, int withinY, StructurePlacement structure)
    {
        int hallY = structure.Height / 2;
        if (withinY == hallY)
        {
            return false;
        }

        int roomWidth = Math.Max(3, (structure.Width - 4) / 4);
        if (withinX <= 0 || withinX >= structure.Width - 1)
        {
            return false;
        }

        int col = (withinX - 1) / roomWidth;
        int localX = (withinX - 1) % roomWidth;
        if (col >= 4 || localX >= roomWidth - 1)
        {
            return false;
        }

        if (withinY < hallY)
        {
            return withinY > 0 && withinY < hallY;
        }

        return withinY > hallY && withinY < structure.Height - 1;
    }

    private static void StampDetailFeatures(
        LocalMap map,
        WorldCoord worldCell,
        StructurePlacement structure,
        StructureBlueprintDefinition blueprint,
        int floorIndex,
        bool surfaceView)
    {
        if (surfaceView)
        {
            return;
        }

        TerrainId? detail = StructureBlueprintCatalog.ParseDetailTerrain(blueprint.DetailTerrain);
        if (detail is null)
        {
            return;
        }

        int detailX = Math.Clamp(blueprint.DoorX, 1, structure.Width - 2);
        int detailY = Math.Clamp(blueprint.DoorY - 2, 1, structure.Height - 2);

        StampRect(
            map,
            worldCell,
            structure.GlobalOriginX,
            structure.GlobalOriginY,
            structure.Width,
            structure.Height,
            (m, localX, localY, withinX, withinY) =>
            {
                if (withinX == detailX && withinY == detailY)
                {
                    m.SetTerrain(localX, localY, detail.Value, TileFlags.BlocksMovement);
                }
            });
    }
}
