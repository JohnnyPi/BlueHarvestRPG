using Game.Simulation.World.Island;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class StructureFinalizeStage
{
    private const uint StageSalt = 99;

    public static void Execute(IslandPlan plan, ulong seed, StructureBlueprintCatalog catalog)
    {
        for (int i = 0; i < plan.Structures.Count; i++)
        {
            StructurePlacement structure = plan.Structures[i];
            var blueprint = catalog.Resolve(structure.Type);
            plan.Structures[i] = structure with
            {
                InstanceId = i + 1,
                BlueprintId = blueprint.Id,
                FloorCount = blueprint.FloorCount,
                BasementCount = blueprint.BasementCount
            };
        }
    }
}
