using Game.Content;
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
        _localMapGenerator = new LocalMapGenerator(_bundle.CreateBlueprintCatalog());
    }

    public RenderSnapshot? Snapshot { get; private set; }
    public Overworld? Overworld { get; private set; }

    public static Overworld GenerateWorld(GameContentBundle bundle, PreviewGenerationRequest request)
    {
        var generator = new IslandWorldGenerator(
            request.Island,
            bundle.CreateBlueprintCatalog(),
            request.BiomeRules);
        return generator.Generate(request.Seed);
    }

    public void ApplyGeneratedWorld(Overworld world)
    {
        Overworld = world;
        var repository = new InMemoryLocalMapRepository(world, _localMapGenerator);
        var session = new GameSession(world, repository);
        OverworldExploration.RevealAll(world);
        session.RevealEntireOverworld();
        session.ViewMode = GameViewMode.Overworld;

        _host = new SimulationHost(world, session, repository) { IsNewGame = true };
        _host.Initialize();
        Snapshot = _host.BuildRenderSnapshot();
    }
}
