using Game.Simulation.AI;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.Input;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Quests;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation;

public sealed class SimulationHost
{
    private readonly Queue<QueuedIntent> _pendingIntents = new();
    private readonly TurnScheduler _turnScheduler = new();
    private int _actionTickCount;
    private RenderSnapshot? _cachedSnapshot;
    private ushort[]? _cellBuffer;
    private byte[]? _tectonicBuffer;
    private byte[]? _riverEdgeBuffer;
    private EntityRenderData[]? _entityBuffer;
    private bool[]? _visibleBuffer;
    private bool[]? _exploredBuffer;

    private readonly record struct QueuedIntent(GameIntent Intent, int TargetX, int TargetY, bool HasTarget);

    public Overworld Overworld { get; }
    public GameSession Session { get; }
    public ILocalMapRepository LocalMapRepository { get; }
    public SimulationClock Clock => _turnScheduler.Clock;

    public RenderViewContent ViewContent { get; set; } = new();

    public string? HoverTooltip { get; set; }

    public bool IsWaitingForPlayerInput =>
        Session.CanIssuePlayerCommand();

    public bool HasPendingSimulationWork =>
        !Session.IsRunComplete &&
        (_pendingIntents.Count > 0 ||
        (Session.HasQueuedMovement && Session.CanAffordQueuedStep()));

    public bool IsNewGame { get; set; }

    public SimulationHost(
        Overworld overworld,
        GameSession session,
        ILocalMapRepository localMapRepository)
    {
        Overworld = overworld;
        Session = session;
        LocalMapRepository = localMapRepository;
    }

    public void Initialize()
    {
        _actionTickCount = 0;
        _pendingIntents.Clear();

        if (IsNewGame)
        {
            Clock.Reset();
            Session.PlayerTurnState.Energy = ActionCostTable.StartingEnergy;
            Session.PlayerTurnState.EnergyRemainder = 0;
        }
        else
        {
            Clock.Restore(Session.WorldTime);
        }

        Session.MarkRenderDirty();
    }

    public void QueueIntent(GameIntent intent)
    {
        if (intent != GameIntent.None && !Session.IsRunComplete)
        {
            _pendingIntents.Enqueue(new QueuedIntent(intent, 0, 0, HasTarget: false));
        }
    }

    public void QueueIntent(GameIntent intent, int targetX, int targetY)
    {
        if (intent != GameIntent.None && !Session.IsRunComplete)
        {
            _pendingIntents.Enqueue(new QueuedIntent(intent, targetX, targetY, HasTarget: true));
        }
    }

    public void Tick()
    {
        if (!HasPendingSimulationWork)
        {
            return;
        }

        bool advanced = false;

        while (_pendingIntents.Count > 0)
        {
            ApplyIntent(_pendingIntents.Dequeue());
            advanced = true;
        }

        if (Session.HasQueuedMovement && Session.CanAffordQueuedStep())
        {
            if (PerformWalkAction())
            {
                advanced = true;
            }
        }

        if (advanced)
        {
            _actionTickCount++;
            Session.MarkRenderDirty();
        }

        if (Session.VisibilityDirty)
        {
            Session.UpdateVisibility();
        }
    }

    private void ApplyIntent(QueuedIntent queued)
    {
        switch (queued.Intent)
        {
            case GameIntent.MoveNorth:
                TryMove(0, -1);
                break;
            case GameIntent.MoveSouth:
                TryMove(0, 1);
                break;
            case GameIntent.MoveWest:
                TryMove(-1, 0);
                break;
            case GameIntent.MoveEast:
                TryMove(1, 0);
                break;
            case GameIntent.MoveNorthWest:
                TryMove(-1, -1);
                break;
            case GameIntent.MoveNorthEast:
                TryMove(1, -1);
                break;
            case GameIntent.MoveSouthWest:
                TryMove(-1, 1);
                break;
            case GameIntent.MoveSouthEast:
                TryMove(1, 1);
                break;
            case GameIntent.Wait:
                PerformWaitAction();
                break;
            case GameIntent.EnterCell:
                if (Session.ViewMode == GameViewMode.Overworld)
                {
                    Session.EnterWorldCell();
                }
                break;
            case GameIntent.LeaveLocalMap:
                if (Session.ViewMode == GameViewMode.LocalMap)
                {
                    Session.LeaveLocalMap();
                }
                break;
            case GameIntent.RemoveTerrain:
                Session.TryRemoveTerrainAtPlayer();
                break;
            case GameIntent.MoveToSelected:
                if (queued.HasTarget)
                {
                    Session.QueueMoveTo(queued.TargetX, queued.TargetY);
                }
                break;
            case GameIntent.EnterSelected:
                if (queued.HasTarget && Session.ViewMode == GameViewMode.Overworld)
                {
                    Session.QueueMoveTo(queued.TargetX, queued.TargetY, enterOnArrival: true);
                }
                break;
            case GameIntent.RemoveTerrainAtSelected:
                if (queued.HasTarget)
                {
                    Session.TryRemoveTerrainAt(queued.TargetX, queued.TargetY);
                }
                break;
            case GameIntent.HarvestAtSelected:
                if (queued.HasTarget)
                {
                    if (!Session.CanHarvestAt(queued.TargetX, queued.TargetY))
                    {
                        Session.MessageLog.Add("Nothing to harvest there.");
                        Session.MarkRenderDirty();
                        break;
                    }

                    if (!Session.PlayerTurnState.TrySpend(ActionCostTable.Harvest))
                    {
                        Session.MessageLog.Add(
                            $"Too exhausted to harvest ({ActionCostTable.Harvest} stamina required).");
                        Session.MarkRenderDirty();
                        break;
                    }

                    if (Session.TryHarvestAt(queued.TargetX, queued.TargetY))
                    {
                        RunScheduler();
                    }
                }
                break;
            case GameIntent.InspectSelected:
                if (queued.HasTarget)
                {
                    string info = Session.InspectTile(queued.TargetX, queued.TargetY);
                    Session.MessageLog.Add(info);
                    Session.MarkRenderDirty();
                }
                break;
            case GameIntent.TransitionBorderNorth:
                if (queued.HasTarget)
                {
                    Session.QueueMoveToBorderTransition(queued.TargetX, queued.TargetY, Direction.North);
                }

                break;
            case GameIntent.TransitionBorderEast:
                if (queued.HasTarget)
                {
                    Session.QueueMoveToBorderTransition(queued.TargetX, queued.TargetY, Direction.East);
                }

                break;
            case GameIntent.TransitionBorderSouth:
                if (queued.HasTarget)
                {
                    Session.QueueMoveToBorderTransition(queued.TargetX, queued.TargetY, Direction.South);
                }

                break;
            case GameIntent.TransitionBorderWest:
                if (queued.HasTarget)
                {
                    Session.QueueMoveToBorderTransition(queued.TargetX, queued.TargetY, Direction.West);
                }

                break;
        }
    }

    private void TryMove(int deltaX, int deltaY)
    {
        if (Session.ViewMode == GameViewMode.Overworld)
        {
            TryMoveOverworld(deltaX, deltaY);
            return;
        }

        if (!Session.TryMoveLocal(deltaX, deltaY))
        {
            return;
        }

        RunScheduler();
    }

    private void TryMoveOverworld(int deltaX, int deltaY)
    {
        if (!Session.CanEnterOverworldStep(deltaX, deltaY))
        {
            var target = new WorldCoord(Session.PlayerWorldPosition.X + deltaX, Session.PlayerWorldPosition.Y + deltaY);
            string blocked = PressureEventResolver.DescribeBlockedTravel(Session, target);
            if (blocked.Length == 0)
            {
                blocked = ScenarioObstacleResolver.DescribeBlockedTravel(Session, target);
            }

            Session.MessageLog.Add(
                blocked.Length > 0 ? blocked : "That terrain cannot be crossed on foot.");
            Session.MarkRenderDirty();
            return;
        }

        int cost = Session.GetOverworldStepCost(deltaX, deltaY);
        if (Session.PlayerTurnState.Energy < cost)
        {
            Session.MessageLog.Add(
                $"Too exhausted to travel ({cost} stamina required, have {Session.PlayerTurnState.Energy}). Press Space to rest.");
            Session.MarkRenderDirty();
            return;
        }

        if (!Session.TryMoveOverworld(deltaX, deltaY))
        {
            return;
        }

        if (!Session.PlayerTurnState.TrySpend(cost))
        {
            return;
        }

        LogOverworldTravel(cost);
        _turnScheduler.AdvanceOverworldTravelStep(Session);
        Session.AdvancePressureClock(cost);
    }

    private bool PerformWalkAction()
    {
        if (Session.ViewMode == GameViewMode.Overworld)
        {
            int cost = Session.GetQueuedOverworldStepCost();
            if (Session.PlayerTurnState.Energy < cost)
            {
                return false;
            }

            if (!Session.PlayerTurnState.TrySpend(cost))
            {
                return false;
            }

            Session.AdvanceMovement();
            LogOverworldTravel(cost);
            _turnScheduler.AdvanceOverworldTravelStep(Session);
            Session.AdvancePressureClock(cost);
            return true;
        }

        Session.AdvanceMovement();
        RunScheduler();
        return true;
    }

    private void LogOverworldTravel(int cost)
    {
        ref WorldCell cell = ref Overworld.GetCell(Session.PlayerWorldPosition);
        Session.MessageLog.Add(
            $"Traveled to {cell.Biome} ({cost} stamina, +{BiomeTraversal.OverworldTravelHours}h).");
    }

    private void PerformWaitAction()
    {
        if (Session.ViewMode == GameViewMode.Overworld)
        {
            _turnScheduler.Clock.Advance();
            Session.NotifyWorldHourElapsed();
            _turnScheduler.RunOverworldRest(Session);
            Session.AdvancePressureClock(ActionCostTable.Wait);
            Session.MessageLog.Add("Rested while traveling the island.");
            Session.MarkRenderDirty();
            return;
        }

        _turnScheduler.Clock.Advance();
        Session.NotifyWorldHourElapsed();
        RunScheduler();
        Session.AdvancePressureClock(ActionCostTable.Wait);
    }

    private void RunScheduler()
    {
        _turnScheduler.RunUntilPlayerReady(
            Session,
            (entity, map) => CreatureAi.TryAct(entity, Session, map, _turnScheduler.Clock.WorldTime));
    }

    public RenderSnapshot BuildRenderSnapshot()
    {
        if (!Session.RenderDirty && _cachedSnapshot is not null)
        {
            return RefreshLiveViews(_cachedSnapshot);
        }

        _cachedSnapshot = Session.ViewMode == GameViewMode.Overworld
            ? BuildOverworldSnapshot()
            : BuildLocalMapSnapshot();

        Session.ClearRenderDirty();
        return _cachedSnapshot;
    }

    private RenderSnapshot RefreshLiveViews(RenderSnapshot cached)
    {
        string terrainOrBiome = ResolveTerrainOrBiome(cached.ViewMode);
        return cached with
        {
            PlayerStatus = BuildPlayerStatus(cached.ViewMode, terrainOrBiome),
            InventoryItems = BuildInventoryItems(),
            QuestItems = BuildQuestItems(),
            CharacterSheet = BuildCharacterSheet(),
            MessageLog = Session.MessageLog.Recent(8),
            HoverTooltip = HoverTooltip,
            PlayerEnergy = Session.PlayerTurnState.Energy,
            WaitingForPlayerInput = IsWaitingForPlayerInput,
            ScenarioMission = Session.RunScenario?.Mission,
            IslandPressure = Session.PressureClock.Pressure,
            OverworldLandmarks = cached.ViewMode == GameViewMode.Overworld
                ? BuildOverworldLandmarks()
                : cached.OverworldLandmarks,
            TravelStaminaPenalty = Session.PressureState.TravelStaminaPenalty,
            EvacHoursRemaining = Session.PressureState.EvacHoursRemaining,
            HazardousTravelX = Session.PressureState.HazardousTravelCell?.X,
            HazardousTravelY = Session.PressureState.HazardousTravelCell?.Y,
            RunOutcome = Session.Outcome,
            EscapeEnding = Session.EscapeEnding,
            RunEndTitle = Session.RunEndTitle,
            RunEndSummary = Session.RunEndSummary
        };
    }

    private string ResolveTerrainOrBiome(GameViewMode viewMode)
    {
        if (viewMode == GameViewMode.Overworld)
        {
            ref WorldCell cell = ref Overworld.GetCell(Session.PlayerWorldPosition);
            return cell.Biome.ToString();
        }

        if (Session.ActiveLocalMap is null)
        {
            return "Unknown";
        }

        int index = Session.ActiveLocalMap.GetIndex(Session.PlayerLocalPosition.X, Session.PlayerLocalPosition.Y);
        return Session.ActiveLocalMap.Terrain[index].ToString();
    }

    private RenderSnapshot BuildOverworldSnapshot()
    {
        int size = Overworld.Width * Overworld.Height;
        _cellBuffer ??= new ushort[size];
        if (_cellBuffer.Length != size)
        {
            _cellBuffer = new ushort[size];
        }

        int index = 0;
        for (int y = 0; y < Overworld.Height; y++)
        {
            for (int x = 0; x < Overworld.Width; x++)
            {
                _cellBuffer[index++] = (ushort)Overworld.GetCellValue(new WorldCoord(x, y)).Biome;
            }
        }

        WorldCoord player = Session.PlayerWorldPosition;
        ref WorldCell cell = ref Overworld.GetCell(player);

        string debugInfo = BuildDebugInfo(
            $"World position: {player.X}, {player.Y}",
            $"Biome: {cell.Biome}",
            $"Elev {cell.Elevation:F2}  Moist {cell.Moisture:F2}  Temp {cell.Temperature:F2}",
            creatureEnergy: null);

        return new RenderSnapshot(
            Title: "Blue Harvest",
            ViewMode: Session.ViewMode,
            GridWidth: Overworld.Width,
            GridHeight: Overworld.Height,
            CellData: _cellBuffer,
            PlayerX: player.X,
            PlayerY: player.Y,
            DebugInfo: debugInfo,
            TickCount: _actionTickCount,
            Entities: [],
            VisibleTiles: null,
            ExploredTiles: Overworld.Explored,
            MessageLog: Session.MessageLog.Recent(8),
            HoverTooltip: HoverTooltip,
            PlayerStatus: BuildPlayerStatus(Session.ViewMode, cell.Biome.ToString()),
            InventoryItems: BuildInventoryItems(),
            QuestItems: BuildQuestItems(),
            CharacterSheet: BuildCharacterSheet(),
            WorldTime: Clock.WorldTime,
            PlayerEnergy: Session.PlayerTurnState.Energy,
            WaitingForPlayerInput: IsWaitingForPlayerInput,
            ScenarioMission: Session.RunScenario?.Mission,
            IslandPressure: Session.PressureClock.Pressure,
            TravelStaminaPenalty: Session.PressureState.TravelStaminaPenalty,
            EvacHoursRemaining: Session.PressureState.EvacHoursRemaining,
            HazardousTravelX: Session.PressureState.HazardousTravelCell?.X,
            HazardousTravelY: Session.PressureState.HazardousTravelCell?.Y,
            OverworldLandmarks: BuildOverworldLandmarks(),
            TectonicBoundaries: BuildTectonicBoundaries(),
            RiverEdgeMask: BuildRiverEdgeMask(),
            RunOutcome: Session.Outcome,
            EscapeEnding: Session.EscapeEnding,
            RunEndTitle: Session.RunEndTitle,
            RunEndSummary: Session.RunEndSummary);
    }

    private OverworldLandmarkView[] BuildOverworldLandmarks()
    {
        if (Overworld.IslandPlan is null)
        {
            return [];
        }

        return OverworldLandmarkCatalog.CollectExploredLandmarks(Overworld, Session.RunScenario)
            .Select(landmark => new OverworldLandmarkView(
                landmark.X,
                landmark.Y,
                landmark.Name,
                landmark.ObjectiveKind))
            .ToArray();
    }

    private byte[]? BuildTectonicBoundaries()
    {
        if (Overworld.IslandPlan is null)
        {
            return null;
        }

        int size = Overworld.Width * Overworld.Height;
        _tectonicBuffer ??= new byte[size];
        if (_tectonicBuffer.Length != size)
        {
            _tectonicBuffer = new byte[size];
        }

        IslandPlan plan = Overworld.IslandPlan;
        int index = 0;
        for (int y = 0; y < Overworld.Height; y++)
        {
            for (int x = 0; x < Overworld.Width; x++)
            {
                _tectonicBuffer[index++] = (byte)plan.GetCell(x, y).BoundaryType;
            }
        }

        return _tectonicBuffer;
    }

    private byte[]? BuildRiverEdgeMask()
    {
        int size = Overworld.Width * Overworld.Height;
        _riverEdgeBuffer ??= new byte[size];
        if (_riverEdgeBuffer.Length != size)
        {
            _riverEdgeBuffer = new byte[size];
        }

        int index = 0;
        for (int y = 0; y < Overworld.Height; y++)
        {
            for (int x = 0; x < Overworld.Width; x++)
            {
                ConnectionFlags flags = Overworld.GetCellValue(new WorldCoord(x, y)).ConnectionFlags;
                _riverEdgeBuffer[index++] = (byte)(((ushort)flags >> 4) & 0x0F);
            }
        }

        return _riverEdgeBuffer;
    }

    private RenderSnapshot BuildLocalMapSnapshot()
    {
        LocalMap map = Session.ActiveLocalMap
            ?? throw new InvalidOperationException("Local map view requires an active local map.");

        int size = LocalMap.Width * LocalMap.Height;
        _cellBuffer ??= new ushort[size];
        _visibleBuffer ??= new bool[size];
        _exploredBuffer ??= new bool[size];

        for (int i = 0; i < map.Terrain.Length; i++)
        {
            _cellBuffer[i] = (ushort)map.Terrain[i];
            _visibleBuffer[i] = Session.VisibleTiles[i];
            _exploredBuffer[i] = map.Explored[i];
        }

        LocalCoord player = Session.PlayerLocalPosition;
        int playerIndex = map.GetIndex(player.X, player.Y);

        Entity? creature = map.Entities.All.FirstOrDefault(entity => entity.Kind == EntityKind.Raptor);
        string? creatureEnergy = creature?.Actor is not null
            ? $"Raptor energy: {creature.Actor.Energy} (speed {creature.Actor.Speed}, {creature.Raptor?.Phase})"
            : null;

        _entityBuffer = map.Entities.All
            .Where(entity => entity.IsActive && entity.Kind != EntityKind.Player)
            .Select(entity => new EntityRenderData(entity.LocalPosition.X, entity.LocalPosition.Y, (int)entity.Kind))
            .ToArray();

        string debugInfo = BuildDebugInfo(
            $"Local map: {map.WorldPosition.X}, {map.WorldPosition.Y}",
            $"Player: {player.X}, {player.Y}",
            $"Terrain: {map.Terrain[playerIndex]}",
            creatureEnergy);

        return new RenderSnapshot(
            Title: "Blue Harvest",
            ViewMode: Session.ViewMode,
            GridWidth: LocalMap.Width,
            GridHeight: LocalMap.Height,
            CellData: _cellBuffer,
            PlayerX: player.X,
            PlayerY: player.Y,
            DebugInfo: debugInfo,
            TickCount: _actionTickCount,
            Entities: _entityBuffer,
            VisibleTiles: _visibleBuffer,
            ExploredTiles: _exploredBuffer,
            MessageLog: Session.MessageLog.Recent(8),
            HoverTooltip: HoverTooltip,
            PlayerStatus: BuildPlayerStatus(Session.ViewMode, map.Terrain[playerIndex].ToString()),
            InventoryItems: BuildInventoryItems(),
            QuestItems: BuildQuestItems(),
            CharacterSheet: BuildCharacterSheet(),
            WorldTime: Clock.WorldTime,
            PlayerEnergy: Session.PlayerTurnState.Energy,
            WaitingForPlayerInput: IsWaitingForPlayerInput,
            ScenarioMission: Session.RunScenario?.Mission,
            IslandPressure: Session.PressureClock.Pressure,
            TravelStaminaPenalty: Session.PressureState.TravelStaminaPenalty,
            EvacHoursRemaining: Session.PressureState.EvacHoursRemaining,
            RunOutcome: Session.Outcome,
            EscapeEnding: Session.EscapeEnding,
            RunEndTitle: Session.RunEndTitle,
            RunEndSummary: Session.RunEndSummary);
    }

    private string BuildDebugInfo(
        string line1,
        string line2,
        string line3,
        string? creatureEnergy)
    {
        string simState = IsWaitingForPlayerInput ? "Waiting for input" : "Simulating";
        var lines = new List<string>
        {
            line1,
            line2,
            line3,
            $"World time: {Clock.WorldTime} (day {Clock.Day} hour {Clock.Hour})",
            $"Player energy: {Session.PlayerTurnState.Energy}",
            $"Sim: {simState}",
        };

        if (creatureEnergy is not null)
        {
            lines.Add(creatureEnergy);
        }

        return string.Join('\n', lines);
    }

    private PlayerStatusView BuildPlayerStatus(GameViewMode viewMode, string terrainOrBiome)
    {
        Entity player = Session.PlayerEntity;
        WorldCoord world = Session.PlayerWorldPosition;
        LocalCoord local = Session.PlayerLocalPosition;

        string locationLabel = viewMode == GameViewMode.Overworld
            ? "Overworld"
            : Session.ActiveLocalMap is not null
                ? $"Local ({Session.ActiveLocalMap.WorldPosition.X}, {Session.ActiveLocalMap.WorldPosition.Y})"
                : "Local";

        return new PlayerStatusView(
            Health: player.Health,
            MaxHealth: player.MaxHealth,
            Energy: Session.PlayerTurnState.Energy,
            Speed: Session.PlayerTurnState.Speed,
            WorldX: world.X,
            WorldY: world.Y,
            LocalX: viewMode == GameViewMode.LocalMap ? local.X : null,
            LocalY: viewMode == GameViewMode.LocalMap ? local.Y : null,
            LocationLabel: locationLabel,
            TerrainOrBiome: terrainOrBiome);
    }

    private InventoryItemView[] BuildInventoryItems()
    {
        return Session.Inventory.Stacks
            .Select(stack =>
            {
                string name = ViewContent.ItemDisplayNames.TryGetValue((int)stack.ItemId, out string? displayName)
                    ? displayName
                    : stack.ItemId.ToString();
                return new InventoryItemView((int)stack.ItemId, name, stack.Count);
            })
            .ToArray();
    }

    private QuestItemView[] BuildQuestItems()
    {
        return Session.QuestLog.Progress
            .OrderBy(entry => QuestSortOrder(entry.QuestId))
            .ThenBy(entry => entry.QuestId, StringComparer.Ordinal)
            .Select(entry =>
            {
                string title;
                string objective;
                int target;

                if (ViewContent.Quests.TryGetValue(entry.QuestId, out var quest))
                {
                    title = quest.Title;
                    objective = quest.Objective;
                    target = quest.Target;
                }
                else
                {
                    title = entry.QuestId;
                    objective = "Unknown objective";
                    target = 1;
                }

                if (Session.RunScenario is not null)
                {
                    (title, objective, target) = ApplyScenarioQuestDetails(entry.QuestId, title, objective, target);
                }

                return new QuestItemView(
                    entry.QuestId,
                    title,
                    objective,
                    entry.Progress,
                    target,
                    entry.State);
            })
            .ToArray();
    }

    private static int QuestSortOrder(string questId)
    {
        return questId switch
        {
            ScenarioQuestIds.Escape => 0,
            ScenarioQuestIds.Mystery => 1,
            ScenarioQuestIds.Endure => 2,
            _ => 10
        };
    }

    private (string Title, string Objective, int Target) ApplyScenarioQuestDetails(
        string questId,
        string title,
        string objective,
        int target)
    {
        RunScenario scenario = Session.RunScenario!;

        if (questId == ScenarioQuestIds.Escape)
        {
            return (
                title,
                $"Reach {scenario.EscapeRoute} ({scenario.EscapeLandmark})",
                target);
        }

        if (questId == ScenarioQuestIds.Mystery)
        {
            return (
                title,
                $"Investigate {scenario.MysteryLandmark}",
                target);
        }

        if (questId == ScenarioQuestIds.Endure)
        {
            return (title, objective, ScenarioQuestIds.EndurePressureTarget);
        }

        return (title, objective, target);
    }

    private CharacterSheetView BuildCharacterSheet()
    {
        Entity player = Session.PlayerEntity;
        var attributes = ViewContent.AttributeDefinitions
            .Select(definition =>
            {
                int value = Session.CharacterProgress.Attributes.TryGetValue(definition.Id, out int stored)
                    ? stored
                    : 0;
                return new AttributeView(definition.Id, definition.DisplayName, value);
            })
            .ToArray();

        int stackCount = Session.Inventory.Stacks.Count;
        int totalCount = Session.Inventory.Stacks.Sum(stack => stack.Count);

        return new CharacterSheetView(
            Level: Session.CharacterProgress.Level,
            Experience: Session.CharacterProgress.Experience,
            Faction: player.Faction.ToString(),
            Attributes: attributes,
            InventoryStackCount: stackCount,
            InventoryTotalCount: totalCount);
    }
}
