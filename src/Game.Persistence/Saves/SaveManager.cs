using Game.Content;
using Game.Content.Definitions;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Serialization;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.World.Island;
using Game.Simulation.UI;
using System.Text.Json;

namespace Game.Persistence.Saves;

public sealed class SaveManager
{
    public const string AppFolderName = "BlueHarvest";
    private const string LegacyAppFolderName = "Rouge";

    private readonly string _saveDirectory;

    public SaveManager(string? saveDirectory = null)
    {
        _saveDirectory = saveDirectory ?? GetDefaultSaveDirectory();
        MigrateLegacySaveIfNeeded();
    }

    public string SaveDirectory => _saveDirectory;

    public static string GetDefaultSaveDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName,
            "saves");
    }

    public string GetSaveFilePath(string slotName = "autosave")
    {
        return Path.Combine(_saveDirectory, $"{slotName}.json");
    }

    public void Save(
        Overworld world,
        GameSession session,
        InMemoryLocalMapRepository repository,
        uint biomeRulesHash,
        string slotName = "autosave",
        long worldTime = 0)
    {
        Directory.CreateDirectory(_saveDirectory);
        session.RefreshPlayerVitals();

        var data = new WorldSaveData
        {
            GeneratorVersion = WorldGeneratorVersion.Current,
            BiomeRulesHash = biomeRulesHash,
            Seed = world.Seed,
            ViewMode = (int)session.ViewMode,
            PlayerWorldX = session.PlayerWorldPosition.X,
            PlayerWorldY = session.PlayerWorldPosition.Y,
            PlayerLocalX = session.PlayerLocalPosition.X,
            PlayerLocalY = session.PlayerLocalPosition.Y,
            PlayerSpeed = session.PlayerTurnState.Speed,
            PlayerEnergy = session.PlayerTurnState.Energy,
            PlayerEnergyRemainder = session.PlayerTurnState.EnergyRemainder,
            PlayerHealth = session.PlayerHealth,
            PlayerMaxHealth = session.PlayerMaxHealth,
            WorldTime = worldTime,
            MovementMode = (int)session.MovementMode,
            MessageLog = session.MessageLog.Recent(MessageLog.MaxPersistedMessages).ToList(),
            MovementPath = session.SnapshotMovementPath()
                .Select(step => new MovementStepSaveData { X = step.X, Y = step.Y })
                .ToList(),
            EnterOnArrival = session.MovementEnterOnArrival,
            TransitionOnArrival = session.MovementTransitionOnArrival,
            TransitionEdgeOnArrival = session.MovementTransitionEdge,
            TransitionBorderX = session.MovementTransitionBorderX,
            TransitionBorderY = session.MovementTransitionBorderY,
            OverworldExplored = world.Explored.ToArray(),
            IslandPressure = session.PressureClock.Pressure,
            PressureLastEventThreshold = session.PressureClock.LastEventThreshold,
            PressureTravelPenalty = session.PressureState.TravelStaminaPenalty,
            EvacHoursRemaining = session.PressureState.EvacHoursRemaining,
            PendingPredatorSpawn = session.PressureState.PendingPredatorSpawn,
            MissedEvacuation = session.PressureState.MissedEvacuation,
            HazardousTravelX = session.PressureState.HazardousTravelCell?.X,
            HazardousTravelY = session.PressureState.HazardousTravelCell?.Y,
            RunScenario = session.RunScenario is null
                ? null
                : new RunScenarioSaveData
                {
                    Mission = session.RunScenario.Mission,
                    StartLocation = session.RunScenario.StartLocation,
                    EscapeRoute = session.RunScenario.EscapeRoute,
                    Obstacle1 = session.RunScenario.Obstacle1,
                    Obstacle2 = session.RunScenario.Obstacle2,
                    Mystery = session.RunScenario.Mystery,
                    FirstEncounter = session.RunScenario.FirstEncounter,
                    IslandSecret = session.RunScenario.IslandSecret,
                    EscapeTargetX = session.RunScenario.EscapeTarget?.X,
                    EscapeTargetY = session.RunScenario.EscapeTarget?.Y,
                    MysteryTargetX = session.RunScenario.MysteryTarget?.X,
                    MysteryTargetY = session.RunScenario.MysteryTarget?.Y,
                    Obstacle1TargetX = session.RunScenario.Obstacle1Target?.X,
                    Obstacle1TargetY = session.RunScenario.Obstacle1Target?.Y,
                    Obstacle2TargetX = session.RunScenario.Obstacle2Target?.X,
                    Obstacle2TargetY = session.RunScenario.Obstacle2Target?.Y,
                    EscapeLandmark = session.RunScenario.EscapeLandmark,
                    MysteryLandmark = session.RunScenario.MysteryLandmark
                },
            LocalMaps = repository.Maps.Values
                .Select(LocalMapSerializer.ToSaveData)
                .ToList(),
            Inventory = session.Inventory.Stacks
                .Select(stack => new ItemStackSaveData { ItemId = (int)stack.ItemId, Count = stack.Count })
                .ToList(),
            QuestProgress = session.QuestLog.Progress
                .Select(entry => new QuestProgressSaveData
                {
                    QuestId = entry.QuestId,
                    State = (int)entry.State,
                    Progress = entry.Progress
                })
                .ToList(),
            CharacterProgress = new CharacterProgressSaveData
            {
                Level = session.CharacterProgress.Level,
                Experience = session.CharacterProgress.Experience,
                Attributes = session.CharacterProgress.Attributes
                    .Select(pair => new AttributeSaveData { Id = pair.Key, Value = pair.Value })
                    .ToList()
            },
            RunOutcome = session.IsRunComplete ? (int)session.Outcome : null,
            EscapeEnding = session.EscapeEnding == EscapeEndingKind.None ? null : (int)session.EscapeEnding,
            RunEndTitle = session.RunEndTitle,
            RunEndSummary = session.RunEndSummary,
            FinaleThreats = session.FinaleThreats.Threats
                .Select(threat => (int)threat)
                .ToList(),
            FirstEncounterTriggered = session.FirstEncounterTriggered,
            ActiveStructureInstanceId = session.ActiveLocalMap?.IsStructureInterior == true
                ? session.ActiveLocalMap.StructureInstanceId
                : null,
            ActiveFloorIndex = session.ActiveLocalMap?.IsStructureInterior == true
                ? session.ActiveLocalMap.FloorIndex
                : null
        };

        if (session.ActiveLocalMap is not null &&
            !repository.Maps.ContainsKey(session.ActiveLocalMap.Key))
        {
            data.LocalMaps.Add(LocalMapSerializer.ToSaveData(session.ActiveLocalMap));
        }

        string path = GetSaveFilePath(slotName);
        File.WriteAllText(path, SaveJson.Serialize(data));
    }

    public bool TryLoad(
        string slotName,
        ILocalMapGenerator generator,
        IslandDefinition islandDefinition,
        StructureBlueprintCatalog blueprintCatalog,
        BiomeRulesDefinition biomeRules,
        uint currentBiomeRulesHash,
        out Overworld world,
        out GameSession session,
        out InMemoryLocalMapRepository repository,
        out long restoredWorldTime,
        out string? failureReason)
    {
        world = null!;
        session = null!;
        repository = null!;
        restoredWorldTime = 0;
        failureReason = null;

        string path = GetSaveFilePath(slotName);
        if (!File.Exists(path))
        {
            return false;
        }

        WorldSaveData data;
        try
        {
            data = SaveJson.Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            failureReason = $"Save file is corrupt or unreadable: {ex.Message}";
            return false;
        }

        if (data.FormatVersionNumber > WorldSaveData.FormatVersion)
        {
            failureReason = $"Save format version {data.FormatVersionNumber} is newer than supported version {WorldSaveData.FormatVersion}.";
            return false;
        }

        if (data.GeneratorVersion != WorldGeneratorVersion.Current)
        {
            failureReason = $"Save generator version {data.GeneratorVersion} does not match current version {WorldGeneratorVersion.Current}.";
            return false;
        }

        if (data.BiomeRulesHash != 0 && data.BiomeRulesHash != currentBiomeRulesHash)
        {
            failureReason = "Save biome rules do not match current content. Overworld would regenerate differently.";
            return false;
        }

        try
        {
            var islandGenerator = new IslandWorldGenerator(islandDefinition, blueprintCatalog, biomeRules);
            world = islandGenerator.Generate(data.Seed);
            repository = new InMemoryLocalMapRepository(world, generator);

            foreach (LocalMapSaveData mapData in data.LocalMaps)
            {
                LocalMap map = LocalMapSerializer.FromSaveData(mapData, world);
                repository.Store(map);

                ref WorldCell cell = ref world.GetCell(map.WorldPosition);
                cell.HasLocalChanges = true;
            }

            session = new GameSession(world, repository)
            {
                ViewMode = (GameViewMode)data.ViewMode,
                PlayerWorldPosition = new WorldCoord(data.PlayerWorldX, data.PlayerWorldY),
                PlayerLocalPosition = new LocalCoord(data.PlayerLocalX, data.PlayerLocalY)
            };

            if (data.PlayerHealth.HasValue)
            {
                session.PlayerHealth = data.PlayerHealth.Value;
            }

            if (data.PlayerMaxHealth.HasValue)
            {
                session.PlayerMaxHealth = data.PlayerMaxHealth.Value;
            }

            if (data.WorldTime.HasValue)
            {
                restoredWorldTime = data.WorldTime.Value;
            }

            if (data.MovementMode.HasValue &&
                Enum.IsDefined(typeof(MovementMode), data.MovementMode.Value))
            {
                session.MovementMode = (MovementMode)data.MovementMode.Value;
            }

            if (data.MessageLog is { Count: > 0 })
            {
                session.MessageLog.Restore(data.MessageLog);
            }

            if (data.MovementPath is { Count: > 0 })
            {
                session.RestoreMovementPath(
                    data.MovementPath.Select(step => (step.X, step.Y)).ToList(),
                    data.EnterOnArrival ?? false,
                    data.TransitionOnArrival ?? false,
                    data.TransitionEdgeOnArrival is int edge && Enum.IsDefined(typeof(Direction), edge)
                        ? (Direction)edge
                        : Direction.North,
                    data.TransitionBorderX ?? 0,
                    data.TransitionBorderY ?? 0);
            }

            if (data.PlayerSpeed.HasValue || data.PlayerEnergy.HasValue)
            {
                session.PlayerTurnState.Speed = data.PlayerSpeed ?? session.PlayerTurnState.Speed;
                session.PlayerTurnState.Energy = data.PlayerEnergy ?? session.PlayerTurnState.Energy;
                session.PlayerTurnState.EnergyRemainder = data.PlayerEnergyRemainder ?? 0;
            }

            if (data.OverworldExplored is not null &&
                data.OverworldExplored.Length == world.Explored.Length)
            {
                Array.Copy(data.OverworldExplored, world.Explored, world.Explored.Length);
            }
            else
            {
                OverworldExploration.InitializeTouristMap(world);
                session.RevealOverworldAroundPlayer();
            }

            if (data.IslandPressure.HasValue)
            {
                session.PressureClock.Restore(data.IslandPressure.Value, data.PressureLastEventThreshold ?? 0);
            }

            session.PressureState.Restore(
                data.PressureTravelPenalty ?? 0,
                data.EvacHoursRemaining,
                data.PendingPredatorSpawn ?? false,
                data.MissedEvacuation ?? false,
                data.HazardousTravelX is int hazardX && data.HazardousTravelY is int hazardY
                    ? new WorldCoord(hazardX, hazardY)
                    : null);

            session.RunScenario = data.RunScenario is null
                ? ScenarioGenerator.Generate(world.Seed, world.IslandPlan)
                : RestoreRunScenario(data.RunScenario, world);

            session.FinaleThreats.Restore(data.FinaleThreats
                .Where(value => Enum.IsDefined(typeof(FinaleThreatId), value))
                .Select(value => (FinaleThreatId)value));

            session.FirstEncounterTriggered = data.FirstEncounterTriggered ?? false;

            foreach (ItemStackSaveData item in data.Inventory)
            {
                session.Inventory.Add(new Game.Simulation.Items.ItemStack((Game.Simulation.Items.ItemId)item.ItemId, item.Count));
            }

            foreach (QuestProgressSaveData quest in data.QuestProgress)
            {
                session.QuestLog.Restore(quest.QuestId, (Game.Simulation.Quests.QuestState)quest.State, quest.Progress);
            }

            if (data.CharacterProgress is not null)
            {
                session.CharacterProgress.Level = data.CharacterProgress.Level;
                session.CharacterProgress.Experience = data.CharacterProgress.Experience;
                foreach (AttributeSaveData attribute in data.CharacterProgress.Attributes)
                {
                    session.CharacterProgress.Attributes[attribute.Id] = attribute.Value;
                }
            }

            if (session.ViewMode == GameViewMode.LocalMap)
            {
                var worldPosition = session.PlayerWorldPosition;
                MapKey activeKey = data.ActiveStructureInstanceId is int structureId && structureId > 0
                    ? new MapKey(worldPosition, structureId, data.ActiveFloorIndex ?? 0)
                    : MapKey.Surface(worldPosition);
                session.ActiveLocalMap = repository.GetOrGenerate(activeKey);
                session.EnsurePlayerEntity();
            }

            if (data.RunOutcome is int outcomeValue && Enum.IsDefined(typeof(RunOutcome), outcomeValue))
            {
                EscapeEndingKind ending = data.EscapeEnding is int endingValue &&
                                          Enum.IsDefined(typeof(EscapeEndingKind), endingValue)
                    ? (EscapeEndingKind)endingValue
                    : EscapeEndingKind.None;

                session.RestoreRunState(
                    (RunOutcome)outcomeValue,
                    ending,
                    data.RunEndTitle,
                    data.RunEndSummary);
            }

            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Failed to restore save: {ex.Message}";
            world = null!;
            session = null!;
            repository = null!;
            return false;
        }
    }

    private void MigrateLegacySaveIfNeeded()
    {
        string legacyDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyAppFolderName,
            "saves");

        string legacyAutosave = Path.Combine(legacyDirectory, "autosave.json");
        string currentAutosave = GetSaveFilePath("autosave");

        if (File.Exists(legacyAutosave) && !File.Exists(currentAutosave))
        {
            Directory.CreateDirectory(_saveDirectory);
            File.Copy(legacyAutosave, currentAutosave);
        }
    }

    private static RunScenario RestoreRunScenario(RunScenarioSaveData data, Overworld world)
    {
        var scenario = new RunScenario
        {
            Mission = data.Mission,
            StartLocation = data.StartLocation,
            EscapeRoute = data.EscapeRoute,
            Obstacle1 = data.Obstacle1,
            Obstacle2 = data.Obstacle2,
            Mystery = data.Mystery,
            FirstEncounter = data.FirstEncounter,
            IslandSecret = data.IslandSecret,
            EscapeLandmark = data.EscapeLandmark ?? string.Empty,
            MysteryLandmark = data.MysteryLandmark ?? string.Empty
        };

        if (data.EscapeTargetX is int escapeX && data.EscapeTargetY is int escapeY)
        {
            scenario.EscapeTarget = new WorldCoord(escapeX, escapeY);
        }

        if (data.MysteryTargetX is int mysteryX && data.MysteryTargetY is int mysteryY)
        {
            scenario.MysteryTarget = new WorldCoord(mysteryX, mysteryY);
        }

        if (data.Obstacle1TargetX is int obstacle1X && data.Obstacle1TargetY is int obstacle1Y)
        {
            scenario.Obstacle1Target = new WorldCoord(obstacle1X, obstacle1Y);
        }

        if (data.Obstacle2TargetX is int obstacle2X && data.Obstacle2TargetY is int obstacle2Y)
        {
            scenario.Obstacle2Target = new WorldCoord(obstacle2X, obstacle2Y);
        }

        if (scenario.EscapeTarget is null || scenario.MysteryTarget is null ||
            scenario.Obstacle1Target is null || scenario.Obstacle2Target is null)
        {
            if (world.IslandPlan is not null)
            {
                if (scenario.EscapeTarget is null || scenario.MysteryTarget is null)
                {
                    ScenarioObjectiveBinder.Bind(scenario, world.IslandPlan);
                }

                if (scenario.Obstacle1Target is null || scenario.Obstacle2Target is null)
                {
                    ScenarioObstacleBinder.Bind(scenario, world.IslandPlan);
                }
            }
        }

        return scenario;
    }
}
