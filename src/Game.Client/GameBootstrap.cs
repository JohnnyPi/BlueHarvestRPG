using Game.Generation.LocalMaps;
using Game.Content;
using Game.Content.Definitions;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Character;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.UI;

namespace Game.Client;

public static class GameBootstrap
{
    public static GameContentBundle LoadContent(string? overrideRoot = null)
    {
        return new ContentLoader(overrideRoot).LoadAll();
    }

    public static SimulationHost CreateSimulationHost(GameContentBundle bundle, string? saveDirectory = null)
    {
        var saveManager = new SaveManager(saveDirectory);
        var blueprintCatalog = bundle.CreateBlueprintCatalog();
        var localMapGenerator = new LocalMapGenerator(blueprintCatalog);
        uint biomeRulesHash = BiomeRulesHash.Compute(bundle.BiomeRules);

        if (saveManager.TryLoad(
                "autosave",
                localMapGenerator,
                bundle.Island,
                blueprintCatalog,
                bundle.BiomeRules,
                biomeRulesHash,
                out Overworld world,
                out GameSession loadedSession,
                out InMemoryLocalMapRepository loadedRepository,
                out long restoredWorldTime,
                out _))
        {
            EnsureCharacterDefaults(loadedSession.CharacterProgress, bundle.CharacterDefaults);
            loadedSession.UpdateVisibility();

            var host = new SimulationHost(world, loadedSession, loadedRepository)
            {
                ViewContent = RenderViewContentFactory.Create(bundle),
                BlueprintCatalog = blueprintCatalog,
                IsNewGame = false,
                RestoredWorldTime = restoredWorldTime
            };
            return host;
        }

        ulong seed = (ulong)DateTime.UtcNow.Ticks;
        var islandGenerator = new IslandWorldGenerator(bundle.Island, blueprintCatalog, bundle.BiomeRules);
        world = islandGenerator.Generate(seed);

        var newRepository = new InMemoryLocalMapRepository(world, localMapGenerator);
        var characterProgress = CharacterProgress.CreateFromDefaults(
            bundle.CharacterDefaults.StartingLevel,
            bundle.CharacterDefaults.StartingExperience,
            bundle.CharacterDefaults.Attributes.Select(attribute => (attribute.Id, attribute.Default)));
        var newSession = new GameSession(world, newRepository, characterProgress);
        OverworldExploration.InitializeTouristMap(world);
        newSession.RevealOverworldAroundPlayer();
        newSession.UpdateVisibility();

        return new SimulationHost(world, newSession, newRepository)
        {
            ViewContent = RenderViewContentFactory.Create(bundle),
            BlueprintCatalog = blueprintCatalog,
            IsNewGame = true
        };
    }

    public static SaveManager CreateSaveManager(string? saveDirectory = null)
    {
        return new SaveManager(saveDirectory);
    }

    public static void SaveGame(SimulationHost host, SaveManager saveManager, GameContentBundle bundle)
    {
        if (host.LocalMapRepository is not InMemoryLocalMapRepository inMemory)
        {
            return;
        }

        uint biomeRulesHash = BiomeRulesHash.Compute(bundle.BiomeRules);
        saveManager.Save(
            host.Overworld,
            host.Session,
            inMemory,
            biomeRulesHash,
            worldTime: host.Clock.WorldTime);
    }

    private static void EnsureCharacterDefaults(CharacterProgress progress, CharacterDefaultsDefinition defaults)
    {
        if (progress.Level <= 0)
        {
            progress.Level = defaults.StartingLevel;
        }

        foreach (AttributeDefaultDefinition attribute in defaults.Attributes)
        {
            if (!progress.Attributes.ContainsKey(attribute.Id))
            {
                progress.Attributes[attribute.Id] = attribute.Default;
            }
        }
    }
}
