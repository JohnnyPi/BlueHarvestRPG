using Game.Content;
using Game.Content.Definitions;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Character;
using Game.Simulation.LocalMaps;
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
        var localMapGenerator = new LocalMapGenerator();
        uint biomeRulesHash = BiomeRulesHash.Compute(bundle.BiomeRules);

        if (saveManager.TryLoad(
                "autosave",
                localMapGenerator,
                bundle.Island,
                biomeRulesHash,
                out Overworld world,
                out GameSession loadedSession,
                out InMemoryLocalMapRepository loadedRepository,
                out _))
        {
            var repository = new PersistentLocalMapRepository(world, localMapGenerator);
            foreach (LocalMap map in loadedRepository.Maps.Values)
            {
                repository.Store(map);
            }

            var session = new GameSession(world, repository, loadedSession.CharacterProgress.Clone())
            {
                ViewMode = loadedSession.ViewMode,
                PlayerWorldPosition = loadedSession.PlayerWorldPosition,
                PlayerLocalPosition = loadedSession.PlayerLocalPosition,
                RunScenario = loadedSession.RunScenario
            };

            foreach (var stack in loadedSession.Inventory.Stacks)
            {
                session.Inventory.Add(stack);
            }

            foreach (var entry in loadedSession.QuestLog.Progress)
            {
                session.QuestLog.Restore(entry.QuestId, entry.State, entry.Progress);
            }

            EnsureCharacterDefaults(session.CharacterProgress, bundle.CharacterDefaults);
            session.PlayerTurnState.Speed = loadedSession.PlayerTurnState.Speed;
            session.PlayerTurnState.Energy = loadedSession.PlayerTurnState.Energy;
            session.PlayerTurnState.EnergyRemainder = loadedSession.PlayerTurnState.EnergyRemainder;
            session.PressureClock.Restore(
                loadedSession.PressureClock.Pressure,
                loadedSession.PressureClock.LastEventThreshold);
            session.PressureState.Restore(
                loadedSession.PressureState.TravelStaminaPenalty,
                loadedSession.PressureState.EvacHoursRemaining,
                loadedSession.PressureState.PendingPredatorSpawn,
                loadedSession.PressureState.MissedEvacuation,
                loadedSession.PressureState.HazardousTravelCell);
            session.RestoreRunState(
                loadedSession.Outcome,
                loadedSession.EscapeEnding,
                loadedSession.RunEndTitle,
                loadedSession.RunEndSummary);
            session.FinaleThreats.Restore(loadedSession.FinaleThreats.Threats);
            session.FirstEncounterTriggered = loadedSession.FirstEncounterTriggered;
            session.PlayerHealth = loadedSession.PlayerHealth;
            session.PlayerMaxHealth = loadedSession.PlayerMaxHealth;
            session.WorldTime = loadedSession.WorldTime;
            session.MessageLog.Restore(loadedSession.MessageLog.Recent(MessageLog.MaxPersistedMessages));
            if (loadedSession.HasQueuedMovement)
            {
                session.RestoreMovementPath(
                    loadedSession.SnapshotMovementPath(),
                    loadedSession.MovementEnterOnArrival,
                    loadedSession.MovementTransitionOnArrival,
                    (Direction)loadedSession.MovementTransitionEdge,
                    loadedSession.MovementTransitionBorderX,
                    loadedSession.MovementTransitionBorderY);
            }

            Array.Copy(loadedSession.Overworld.Explored, world.Explored, world.Explored.Length);

            if (session.ViewMode == GameViewMode.LocalMap)
            {
                session.ActiveLocalMap = repository.GetOrGenerate(session.PlayerWorldPosition);
                session.EnsurePlayerEntity();
                session.UpdateVisibility();
            }

            var host = new SimulationHost(world, session, repository)
            {
                ViewContent = RenderViewContentFactory.Create(bundle),
                IsNewGame = false
            };
            host.Initialize();
            return host;
        }

        ulong seed = (ulong)DateTime.UtcNow.Ticks;
        var islandGenerator = new IslandWorldGenerator(bundle.Island);
        world = islandGenerator.Generate(seed);

        var newRepository = new PersistentLocalMapRepository(world, localMapGenerator);
        var characterProgress = CharacterProgress.CreateFromDefaults(
            bundle.CharacterDefaults.StartingLevel,
            bundle.CharacterDefaults.StartingExperience,
            bundle.CharacterDefaults.Attributes.Select(attribute => (attribute.Id, attribute.Default)));
        var newSession = new GameSession(world, newRepository, characterProgress);
        OverworldExploration.InitializeTouristMap(world);
        newSession.RevealOverworldAroundPlayer();

        return new SimulationHost(world, newSession, newRepository)
        {
            ViewContent = RenderViewContentFactory.Create(bundle),
            IsNewGame = true
        };
    }

    public static SaveManager CreateSaveManager(string? saveDirectory = null)
    {
        return new SaveManager(saveDirectory);
    }

    public static void SaveGame(SimulationHost host, SaveManager saveManager, GameContentBundle bundle)
    {
        uint biomeRulesHash = BiomeRulesHash.Compute(bundle.BiomeRules);

        if (host.LocalMapRepository is PersistentLocalMapRepository persistent)
        {
            saveManager.Save(
                host.Overworld,
                host.Session,
                persistent.Inner,
                biomeRulesHash,
                worldTime: host.Clock.WorldTime);
            return;
        }

        if (host.LocalMapRepository is InMemoryLocalMapRepository inMemory)
        {
            saveManager.Save(
                host.Overworld,
                host.Session,
                inMemory,
                biomeRulesHash,
                worldTime: host.Clock.WorldTime);
        }
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
