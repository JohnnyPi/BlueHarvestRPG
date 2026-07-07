using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Simulation.World;

public static class TileTransitionResolver
{
    public static bool TryResolveAt(
        Overworld overworld,
        LocalMap map,
        LocalCoord position,
        StructureBlueprintCatalog blueprintCatalog,
        out TileTransition transition,
        TileTransitionKind? preferredKind = null)
    {
        transition = default;

        if (overworld.IslandPlan is null || !map.Contains(position))
        {
            return false;
        }

        int index = map.GetIndex(position.X, position.Y);
        TerrainId terrain = map.Terrain[index];

        if (map.IsSurface)
        {
            return TryResolveSurfaceTransition(
                overworld,
                map,
                position,
                terrain,
                blueprintCatalog,
                out transition);
        }

        return TryResolveInteriorTransition(
            overworld,
            map,
            position,
            terrain,
            blueprintCatalog,
            out transition,
            preferredKind);
    }

    private static bool TryResolveSurfaceTransition(
        Overworld overworld,
        LocalMap map,
        LocalCoord position,
        TerrainId terrain,
        StructureBlueprintCatalog blueprintCatalog,
        out TileTransition transition)
    {
        transition = default;

        if (terrain is not (TerrainId.Door or TerrainId.StairsUp))
        {
            return false;
        }

        StructurePlacement? structure = StructurePlacementQueries.FindAtLocalPosition(
            overworld.IslandPlan!,
            map.WorldPosition,
            position);

        if (structure is null || structure.InstanceId <= 0)
        {
            return false;
        }

        var blueprint = blueprintCatalog.ResolveById(structure.BlueprintId);
        LocalCoord interiorDoor = StructurePlacementQueries.ToLocalCoord(
            map.WorldPosition,
            structure,
            blueprint.DoorX,
            blueprint.DoorY);

        transition = new TileTransition(
            TileTransitionKind.EnterStructure,
            new MapKey(map.WorldPosition, structure.InstanceId, 0),
            interiorDoor,
            RequiresRope: false);

        return true;
    }

    private static bool TryResolveInteriorTransition(
        Overworld overworld,
        LocalMap map,
        LocalCoord position,
        TerrainId terrain,
        StructureBlueprintCatalog blueprintCatalog,
        out TileTransition transition,
        TileTransitionKind? preferredKind = null)
    {
        transition = default;

        StructurePlacement? structure = StructurePlacementQueries.FindByInstanceId(
            overworld.IslandPlan!,
            map.StructureInstanceId);

        if (structure is null)
        {
            return false;
        }

        var blueprint = blueprintCatalog.ResolveById(structure.BlueprintId);
        LocalCoord stairLocal = StructurePlacementQueries.ToLocalCoord(
            map.WorldPosition,
            structure,
            blueprint.StairX,
            blueprint.StairY);
        LocalCoord doorLocal = StructurePlacementQueries.ToLocalCoord(
            map.WorldPosition,
            structure,
            blueprint.DoorX,
            blueprint.DoorY);

        if (terrain == TerrainId.StructureExit || (terrain == TerrainId.Door && map.FloorIndex == 0))
        {
            if (position != doorLocal)
            {
                return false;
            }

            LocalCoord surfaceDoor = StructurePlacementQueries.ToLocalCoord(
                map.WorldPosition,
                structure,
                blueprint.DoorX,
                blueprint.DoorY);
            transition = new TileTransition(
                TileTransitionKind.ExitStructure,
                MapKey.Surface(map.WorldPosition),
                surfaceDoor,
                RequiresRope: false);
            return true;
        }

        if ((terrain == TerrainId.StairsUp || terrain == TerrainId.StairsDown) && position == stairLocal)
        {
            bool wantDown = preferredKind == TileTransitionKind.StairsDown;
            bool wantUp = preferredKind == TileTransitionKind.StairsUp || preferredKind is null;

            if (wantDown)
            {
                int targetFloor = map.FloorIndex - 1;
                if (!structure.HasFloor(targetFloor))
                {
                    return false;
                }

                transition = new TileTransition(
                    TileTransitionKind.StairsDown,
                    new MapKey(map.WorldPosition, structure.InstanceId, targetFloor),
                    stairLocal,
                    RequiresRope: false);
                return true;
            }

            if (wantUp)
            {
                int targetFloor = map.FloorIndex + 1;
                if (!structure.HasFloor(targetFloor))
                {
                    return false;
                }

                transition = new TileTransition(
                    TileTransitionKind.StairsUp,
                    new MapKey(map.WorldPosition, structure.InstanceId, targetFloor),
                    stairLocal,
                    RequiresRope: false);
                return true;
            }
        }

        if (terrain == TerrainId.Window &&
            blueprintCatalog.TryGetRopeExit(structure, map.FloorIndex, out int ropeX, out int ropeY))
        {
            LocalCoord ropeLocal = StructurePlacementQueries.ToLocalCoord(
                map.WorldPosition,
                structure,
                ropeX,
                ropeY);
            if (position != ropeLocal)
            {
                return false;
            }

            LocalCoord surfaceDoor = StructurePlacementQueries.ToLocalCoord(
                map.WorldPosition,
                structure,
                blueprint.DoorX,
                blueprint.DoorY);
            transition = new TileTransition(
                TileTransitionKind.RopeDescent,
                MapKey.Surface(map.WorldPosition),
                surfaceDoor,
                RequiresRope: true);
            return true;
        }

        return false;
    }
}
