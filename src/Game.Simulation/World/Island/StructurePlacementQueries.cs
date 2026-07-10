using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.World.Island;

public static class StructurePlacementQueries
{
    public static StructurePlacement? FindByInstanceId(IslandPlan plan, int instanceId)
    {
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (structure.InstanceId == instanceId)
            {
                return structure;
            }
        }

        return null;
    }

    public static StructurePlacement? FindAtGlobalTile(IslandPlan plan, int globalX, int globalY)
    {
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (globalX >= structure.GlobalOriginX &&
                globalX < structure.GlobalOriginX + structure.Width &&
                globalY >= structure.GlobalOriginY &&
                globalY < structure.GlobalOriginY + structure.Height)
            {
                return structure;
            }
        }

        return null;
    }

    public static StructurePlacement? FindAtLocalPosition(
        IslandPlan plan,
        WorldCoord worldCell,
        LocalCoord localPosition)
    {
        int globalX = worldCell.X * LocalMap.Width + localPosition.X;
        int globalY = worldCell.Y * LocalMap.Height + localPosition.Y;
        return FindAtGlobalTile(plan, globalX, globalY);
    }

    public static WorldCoord OriginCell(StructurePlacement structure)
    {
        return new WorldCoord(
            structure.GlobalOriginX / LocalMap.Width,
            structure.GlobalOriginY / LocalMap.Height);
    }

    /// <summary>
    /// Door position within the structure footprint. Uses the blueprint door when it lies on the
    /// footprint perimeter; for enlarged (multi-cell) placements it falls back to bottom-center,
    /// matching the blueprint convention.
    /// </summary>
    public static (int X, int Y) DoorWithin(StructurePlacement structure, StructureBlueprintDefinition blueprint)
    {
        if (IsOnPerimeter(blueprint.DoorX, blueprint.DoorY, structure.Width, structure.Height))
        {
            return (blueprint.DoorX, blueprint.DoorY);
        }

        return (structure.Width / 2, structure.Height - 1);
    }

    public static (int GlobalX, int GlobalY) DoorGlobal(StructurePlacement structure, StructureBlueprintDefinition blueprint)
    {
        (int doorX, int doorY) = DoorWithin(structure, blueprint);
        return (structure.GlobalOriginX + doorX, structure.GlobalOriginY + doorY);
    }

    public static WorldCoord DoorCell(StructurePlacement structure, StructureBlueprintDefinition blueprint)
    {
        (int globalX, int globalY) = DoorGlobal(structure, blueprint);
        return new WorldCoord(globalX / LocalMap.Width, globalY / LocalMap.Height);
    }

    public static LocalCoord SurfaceDoorLocal(StructurePlacement structure, StructureBlueprintDefinition blueprint)
    {
        (int globalX, int globalY) = DoorGlobal(structure, blueprint);
        WorldCoord doorCell = DoorCell(structure, blueprint);
        return new LocalCoord(
            globalX - doorCell.X * LocalMap.Width,
            globalY - doorCell.Y * LocalMap.Height);
    }

    /// <summary>
    /// Interior floors are capped at one local map. Returns the interior footprint's local
    /// offset and size within the origin-cell interior map. Placements that fit inside their
    /// origin cell keep their natural offset, so existing single-cell interiors are unchanged.
    /// </summary>
    public static (int OffsetX, int OffsetY, int Width, int Height) InteriorFrame(StructurePlacement structure)
    {
        WorldCoord originCell = OriginCell(structure);
        int width = Math.Min(structure.Width, LocalMap.Width);
        int height = Math.Min(structure.Height, LocalMap.Height);
        int offsetX = Math.Clamp(structure.GlobalOriginX - originCell.X * LocalMap.Width, 0, LocalMap.Width - width);
        int offsetY = Math.Clamp(structure.GlobalOriginY - originCell.Y * LocalMap.Height, 0, LocalMap.Height - height);
        return (offsetX, offsetY, width, height);
    }

    /// <summary>
    /// Creates the placement used to stamp interior floor maps: same structure, but with the
    /// footprint capped to the interior frame and anchored inside the origin cell.
    /// </summary>
    public static StructurePlacement InteriorPlacement(StructurePlacement structure)
    {
        (int offsetX, int offsetY, int width, int height) = InteriorFrame(structure);
        if (width == structure.Width && height == structure.Height)
        {
            return structure;
        }

        WorldCoord originCell = OriginCell(structure);
        return structure with
        {
            GlobalOriginX = originCell.X * LocalMap.Width + offsetX,
            GlobalOriginY = originCell.Y * LocalMap.Height + offsetY,
            Width = width,
            Height = height
        };
    }

    public static LocalCoord InteriorDoorLocal(StructurePlacement structure, StructureBlueprintDefinition blueprint)
    {
        StructurePlacement interior = InteriorPlacement(structure);
        (int offsetX, int offsetY, _, _) = InteriorFrame(structure);
        (int doorX, int doorY) = DoorWithin(interior, blueprint);
        return new LocalCoord(offsetX + doorX, offsetY + doorY);
    }

    public static LocalCoord InteriorLocal(StructurePlacement structure, int withinX, int withinY)
    {
        (int offsetX, int offsetY, _, _) = InteriorFrame(structure);
        return new LocalCoord(offsetX + withinX, offsetY + withinY);
    }

    private static bool IsOnPerimeter(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        return x == 0 || y == 0 || x == width - 1 || y == height - 1;
    }

    public static LocalCoord ToLocalCoord(
        WorldCoord worldCell,
        StructurePlacement structure,
        int withinX,
        int withinY)
    {
        int globalX = structure.GlobalOriginX + withinX;
        int globalY = structure.GlobalOriginY + withinY;
        return new LocalCoord(
            globalX - worldCell.X * LocalMap.Width,
            globalY - worldCell.Y * LocalMap.Height);
    }

    public static LocalCoord ToLocalCoordFromOrigin(
        StructurePlacement structure,
        int withinX,
        int withinY)
    {
        WorldCoord originCell = OriginCell(structure);
        return new LocalCoord(
            structure.GlobalOriginX + withinX - originCell.X * LocalMap.Width,
            structure.GlobalOriginY + withinY - originCell.Y * LocalMap.Height);
    }
}
