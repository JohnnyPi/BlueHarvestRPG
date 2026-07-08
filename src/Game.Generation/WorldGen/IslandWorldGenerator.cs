using Game.Simulation.World.Island;
using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.WorldGen;

public sealed class IslandWorldGenerator
{
    private readonly IslandPlanner _planner;
    private readonly IslandDefinition _config;

    public IslandWorldGenerator(IslandDefinition config, StructureBlueprintCatalog? blueprintCatalog = null)
    {
        _config = config;
        _planner = new IslandPlanner(config, blueprintCatalog);
    }

    public Overworld Generate(ulong seed)
    {
        int size = _config.OverworldSize;
        return Generate(size, size, seed);
    }

    public Overworld Generate(int width, int height, ulong seed)
    {
        IslandPlan plan = _planner.Generate(width, height, seed);
        var world = new Overworld(width, height, seed)
        {
            IslandPlan = plan
        };

        ApplyPlanToWorld(world, plan);
        RegionalFeatureGraph.ApplyRivers(world, plan, _config);
        FacilityRoadGraphApplier.ApplyToOverworld(world, plan, _config.RoadWidth);
        if (_config.UseLegacyRandomRoads)
        {
            RegionalFeatureGraph.ApplyRoads(world);
        }

        return world;
    }

    private static void ApplyPlanToWorld(Overworld world, IslandPlan plan)
    {
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData source = ref plan.GetCell(x, y);
                ref WorldCell target = ref world.GetCell(new WorldCoord(x, y));

                target.Elevation = source.Elevation;
                target.Moisture = source.Moisture;
                target.Temperature = source.Temperature;
                target.Biome = source.Biome;
                target.HasLocalChanges = false;
            }
        }
    }
}
