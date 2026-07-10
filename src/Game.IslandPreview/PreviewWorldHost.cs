using Game.Content;
using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.IslandPreview.Generation;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;

namespace Game.IslandPreview;

public sealed class PreviewWorldHost
{
    private readonly GameContentBundle _bundle;
    private readonly LocalMapGenerator _localMapGenerator;
    private SimulationHost? _host;

    public PreviewWorldHost(GameContentBundle bundle)
    {
        _bundle = bundle;
        _localMapGenerator = new LocalMapGenerator(
            _bundle.CreateBlueprintCatalog(),
            _bundle.BiomeRules);
    }

    public RenderSnapshot? Snapshot { get; private set; }
    public Overworld? Overworld { get; private set; }

    public static Overworld GenerateWorld(GameContentBundle bundle, PreviewGenerationRequest request)
    {
        var diagnostics = new GenerationDiagnosticsOptions
        {
            CaptureSnapshots = true,
            RunQualityGate = false,
            Progress = request.Progress,
        };
        var generator = new IslandWorldGenerator(
            request.Island,
            bundle.CreateBlueprintCatalog(),
            request.BiomeRules,
            diagnostics);
        return generator.Generate(request.Seed);
    }

    public void ApplyGeneratedWorld(Overworld world, BiomeRulesDefinition? biomeRules = null)
    {
        Overworld = world;
        LocalMapGenerator generator = biomeRules is null
            ? _localMapGenerator
            : new LocalMapGenerator(_bundle.CreateBlueprintCatalog(), biomeRules);
        var repository = new InMemoryLocalMapRepository(world, generator);
        var session = new GameSession(world, repository);
        OverworldExploration.RevealAll(world);
        session.RevealEntireOverworld();
        session.ViewMode = GameViewMode.Overworld;

        _host = new SimulationHost(world, session, repository) { IsNewGame = true };
        _host.Initialize();
        Snapshot = _host.BuildRenderSnapshot();
    }
}
