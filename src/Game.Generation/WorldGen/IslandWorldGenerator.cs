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
    private readonly GenerationDiagnosticsOptions _diagnostics;

    public IslandWorldGenerator(
        IslandDefinition config,
        StructureBlueprintCatalog? blueprintCatalog = null,
        BiomeRulesDefinition? biomeRules = null,
        GenerationDiagnosticsOptions? diagnostics = null)
    {
        _config = config;
        _diagnostics = diagnostics ?? new GenerationDiagnosticsOptions();
        _planner = new IslandPlanner(config, blueprintCatalog, biomeRules, _diagnostics);
    }

    public Overworld Generate(ulong seed)
    {
        int size = _config.OverworldSize;
        return Generate(size, size, seed);
    }

    public Overworld Generate(int width, int height, ulong seed)
    {
        IslandPlan plan = RunStage("Island plan", () => _planner.Generate(width, height, seed));
        var world = new Overworld(width, height, seed)
        {
            IslandPlan = plan
        };

        RunStage("Apply plan to world", () => ApplyPlanToWorld(world, plan));
        RunStage("Rivers", () => RegionalFeatureGraph.ApplyRivers(world, plan, _config));
        RunStage("Facility roads", () => FacilityRoadGraphApplier.ApplyToOverworld(world, plan, _config.RoadWidth));
        if (_config.UseLegacyRandomRoads)
        {
            RunStage("Legacy roads", () => RegionalFeatureGraph.ApplyRoads(world));
        }

        return world;
    }

    private void RunStage(string name, Action action)
    {
        if (_diagnostics.Progress is IslandGenerationProgressReporter progress)
        {
            progress.RunStage(name, action);
            return;
        }

        action();
    }

    private T RunStage<T>(string name, Func<T> action)
    {
        if (_diagnostics.Progress is IslandGenerationProgressReporter progress)
        {
            T result = default!;
            progress.RunStage(name, () => result = action());
            return result;
        }

        return action();
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
