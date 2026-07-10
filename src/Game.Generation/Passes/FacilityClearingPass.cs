using Game.Generation.Island;
using Game.Content.Definitions;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class FacilityClearingPass : IGenerationPass
{
    private const int Margin = 6;

    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null || context.BlueprintCatalog is null)
        {
            return;
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
            ClearStructureYard(map, context.WorldCoordinate, structure, blueprint);
        }

        foreach (RuinSite ruin in context.IslandPlan.RuinSites)
        {
            if (!CoordinateMath.OverlapsCell(
                    ruin.GlobalOriginX,
                    ruin.GlobalOriginY,
                    ruin.Width,
                    ruin.Height,
                    context.WorldCoordinate))
            {
                continue;
            }

            ClearRuinYard(map, context.WorldCoordinate, ruin);
        }
    }

    private static void ClearStructureYard(
        LocalMap map,
        WorldCoord worldCell,
        StructurePlacement structure,
        StructureBlueprintDefinition blueprint)
    {
        int cellMinX = worldCell.X * LocalMap.Width;
        int cellMinY = worldCell.Y * LocalMap.Height;

        int minGx = structure.GlobalOriginX - Margin;
        int minGy = structure.GlobalOriginY - Margin;
        int maxGx = structure.GlobalOriginX + structure.Width + Margin;
        int maxGy = structure.GlobalOriginY + structure.Height + Margin;

        TerrainId yardTerrain = structure.Type is StructureType.Helipad or StructureType.MaintenanceCompound
            ? TerrainId.Concrete
            : TerrainId.Grass;

        for (int gy = minGy; gy <= maxGy; gy++)
        {
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                if (gx < cellMinX || gx >= cellMinX + LocalMap.Width ||
                    gy < cellMinY || gy >= cellMinY + LocalMap.Height)
                {
                    continue;
                }

                int localX = gx - cellMinX;
                int localY = gy - cellMinY;
                int index = map.GetIndex(localX, localY);
                TerrainId terrain = map.Terrain[index];
                if (terrain is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Floor or TerrainId.Door
                    or TerrainId.Concrete or TerrainId.Dock or TerrainId.StairsUp or TerrainId.StairsDown
                    or TerrainId.StructureExit or TerrainId.Window)
                {
                    continue;
                }

                map.SetTerrain(localX, localY, yardTerrain, TileFlags.None);
            }
        }

        (int doorX, int doorY) = StructurePlacementQueries.DoorWithin(structure, blueprint);
        LocalCoord door = StructurePlacementQueries.ToLocalCoord(
            worldCell,
            structure,
            doorX,
            doorY);
        CarveApproach(map, door, yardTerrain);
    }

    private static void ClearRuinYard(LocalMap map, WorldCoord worldCell, RuinSite ruin)
    {
        int cellMinX = worldCell.X * LocalMap.Width;
        int cellMinY = worldCell.Y * LocalMap.Height;

        int minGx = ruin.GlobalOriginX - Margin;
        int minGy = ruin.GlobalOriginY - Margin;
        int maxGx = ruin.GlobalOriginX + ruin.Width + Margin;
        int maxGy = ruin.GlobalOriginY + ruin.Height + Margin;

        for (int gy = minGy; gy <= maxGy; gy++)
        {
            for (int gx = minGx; gx <= maxGx; gx++)
            {
                if (gx < cellMinX || gx >= cellMinX + LocalMap.Width ||
                    gy < cellMinY || gy >= cellMinY + LocalMap.Height)
                {
                    continue;
                }

                int localX = gx - cellMinX;
                int localY = gy - cellMinY;
                int index = map.GetIndex(localX, localY);
                if (map.Terrain[index] is TerrainId.Wall or TerrainId.RuinStone)
                {
                    continue;
                }

                map.SetTerrain(localX, localY, TerrainId.Grass, TileFlags.None);
            }
        }

        int entryGlobalX = ruin.GlobalOriginX + ruin.Width / 2;
        int entryGlobalY = ruin.GlobalOriginY + ruin.Height - 1;
        var entry = new LocalCoord(entryGlobalX - cellMinX, entryGlobalY - cellMinY);
        if (map.Contains(entry))
        {
            CarveApproach(map, entry, TerrainId.Grass);
        }
    }

    private static void CarveApproach(LocalMap map, LocalCoord door, TerrainId terrain)
    {
        foreach ((int dx, int dy) in new (int, int)[] { (0, 1), (0, -1), (1, 0), (-1, 0), (0, 2), (0, -2) })
        {
            int x = door.X + dx;
            int y = door.Y + dy;
            if (!map.Contains(new LocalCoord(x, y)))
            {
                continue;
            }

            int index = map.GetIndex(x, y);
            if (map.Terrain[index] is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Fence)
            {
                continue;
            }

            map.SetTerrain(x, y, terrain, TileFlags.None);
        }
    }
}
