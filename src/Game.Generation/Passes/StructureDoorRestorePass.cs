using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class StructureDoorRestorePass : IGenerationPass
{
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
            LocalCoord door = StructurePlacementQueries.ToLocalCoord(
                context.WorldCoordinate,
                structure,
                blueprint.DoorX,
                blueprint.DoorY);

            if (!map.Contains(door))
            {
                continue;
            }

            map.SetTerrain(door.X, door.Y, TerrainId.Door, TileFlags.None);
        }
    }
}
