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
}
