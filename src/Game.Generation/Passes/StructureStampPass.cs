using Game.Simulation.World.Island;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class StructureStampPass : IGenerationPass
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
            StructureStampHelper.StampBuilding(
                map,
                context.WorldCoordinate,
                structure,
                blueprint,
                floorIndex: 0,
                surfaceView: true);
        }
    }
}
