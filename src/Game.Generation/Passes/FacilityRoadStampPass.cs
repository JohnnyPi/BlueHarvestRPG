using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class FacilityRoadStampPass : IGenerationPass
{
    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null || context.BlueprintCatalog is null)
        {
            return;
        }

        int cellMinX = context.WorldCoordinate.X * LocalMap.Width;
        int cellMinY = context.WorldCoordinate.Y * LocalMap.Height;
        int cellMaxX = cellMinX + LocalMap.Width - 1;
        int cellMaxY = cellMinY + LocalMap.Height - 1;

        foreach ((int globalX, int globalY) in context.IslandPlan.RoadGraph.GlobalPathTiles)
        {
            if (globalX < cellMinX || globalX > cellMaxX || globalY < cellMinY || globalY > cellMaxY)
            {
                continue;
            }

            int localX = globalX - cellMinX;
            int localY = globalY - cellMinY;
            if (IsStructureDoorTile(context, cellMinX, cellMinY, localX, localY))
            {
                continue;
            }

            StampRoad(map, localX, localY);
        }

        foreach (StructurePlacement structure in context.IslandPlan.Structures)
        {
            if (!CoordinateMath.OverlapsCell(
                    structure.GlobalOriginX,
                    structure.GlobalOriginY,
                    structure.Width,
                    structure.Height,
                    context.WorldCoordinate))
            {
                continue;
            }

            var blueprint = context.BlueprintCatalog.ResolveById(structure.BlueprintId);
            (int doorGlobalX, int doorGlobalY) = StructurePlacementQueries.DoorGlobal(structure, blueprint);
            StampApproachFromRoad(
                map,
                context,
                cellMinX,
                cellMinY,
                doorGlobalX,
                doorGlobalY);
        }
    }

    private static void StampApproachFromRoad(
        LocalMap map,
        LocalGenerationContext context,
        int cellMinX,
        int cellMinY,
        int doorGlobalX,
        int doorGlobalY)
    {
        if (doorGlobalX < cellMinX || doorGlobalX >= cellMinX + LocalMap.Width ||
            doorGlobalY < cellMinY || doorGlobalY >= cellMinY + LocalMap.Height)
        {
            return;
        }

        int doorLocalX = doorGlobalX - cellMinX;
        int doorLocalY = doorGlobalY - cellMinY;

        (int nearestGx, int nearestGy)? nearestRoad = null;
        int bestDist = int.MaxValue;
        foreach ((int globalX, int globalY) in context.IslandPlan!.RoadGraph.GlobalPathTiles)
        {
            if (globalX < cellMinX || globalX >= cellMinX + LocalMap.Width ||
                globalY < cellMinY || globalY >= cellMinY + LocalMap.Height)
            {
                continue;
            }

            int dist = Math.Abs(globalX - doorGlobalX) + Math.Abs(globalY - doorGlobalY);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearestRoad = (globalX, globalY);
            }
        }

        if (nearestRoad is null)
        {
            return;
        }

        int x = nearestRoad.Value.nearestGx - cellMinX;
        int y = nearestRoad.Value.nearestGy - cellMinY;
        while (x != doorLocalX)
        {
            if (!IsStructureDoorTile(context, cellMinX, cellMinY, x, y))
            {
                StampRoad(map, x, y);
            }

            x += Math.Sign(doorLocalX - x);
        }

        while (y != doorLocalY)
        {
            if (!IsStructureDoorTile(context, cellMinX, cellMinY, x, y))
            {
                StampRoad(map, x, y);
            }

            y += Math.Sign(doorLocalY - y);
        }
    }

    private static bool IsStructureDoorTile(
        LocalGenerationContext context,
        int cellMinX,
        int cellMinY,
        int localX,
        int localY)
    {
        if (context.IslandPlan is null || context.BlueprintCatalog is null)
        {
            return false;
        }

        int globalX = cellMinX + localX;
        int globalY = cellMinY + localY;

        foreach (StructurePlacement structure in context.IslandPlan.Structures)
        {
            var blueprint = context.BlueprintCatalog.ResolveById(structure.BlueprintId);
            (int doorGlobalX, int doorGlobalY) = StructurePlacementQueries.DoorGlobal(structure, blueprint);
            if (globalX == doorGlobalX && globalY == doorGlobalY)
            {
                return true;
            }
        }

        return false;
    }

    private static void StampRoad(LocalMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
        {
            return;
        }

        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain == TerrainId.ShallowFord)
        {
            return;
        }

        if (terrain is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Door or TerrainId.Fence
            or TerrainId.Floor or TerrainId.StructureExit or TerrainId.Dock)
        {
            return;
        }

        map.SetTerrain(x, y, TerrainId.Road, TileFlags.None);
    }
}
