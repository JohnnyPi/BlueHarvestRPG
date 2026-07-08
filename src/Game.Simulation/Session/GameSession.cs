using Game.Simulation.AI;
using Game.Simulation.Character;
using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Quests;
using Game.Simulation.Scenarios;
using Game.Simulation.Session.Services;
using Game.Simulation.Time;
using Game.Simulation.UI;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Session;

public sealed class GameSession
{
    private readonly EntityRegistry _entityRegistry;
    private readonly Queue<(int X, int Y)> _movementPath = new();
    private readonly MapTransitionService _transitions = new();
    private readonly MovementService _movement;
    private readonly InteractionResolver _interactions = new();
    private readonly StructureTransitionService _structureTransitions = new();
    private bool[] _visibleTiles = new bool[LocalMap.Width * LocalMap.Height];

    public GameSession(Overworld overworld, ILocalMapRepository localMapRepository, CharacterProgress? characterProgress = null)
    {
        Overworld = overworld;
        LocalMapRepository = localMapRepository;
        _movement = new MovementService(_transitions);
        _entityRegistry = new EntityRegistry(overworld, localMapRepository);
        ViewMode = GameViewMode.Overworld;
        RunScenario = ScenarioGenerator.Generate(overworld.Seed, overworld.IslandPlan);
        PlayerWorldPosition = ResolveStartingWorldPosition(overworld, RunScenario);
        PlayerLocalPosition = new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2);
        Inventory = new Inventory();
        QuestLog = new QuestLog();
        CharacterProgress = characterProgress ?? new CharacterProgress();
        StartScenarioQuests();
        RevealOverworldAroundPlayer();
        UpdateVisibility();
    }

    private void StartScenarioQuests()
    {
        QuestLog.Start(ScenarioQuestIds.Escape);
        QuestLog.Start(ScenarioQuestIds.Mystery);
        QuestLog.Start(ScenarioQuestIds.Endure);
        QuestLog.Start("gather_wood");
        QuestLog.Start("first_kill");
    }

    private static WorldCoord ResolveStartingWorldPosition(Overworld overworld, RunScenario? scenario = null)
    {
        if (overworld.IslandPlan is not null && scenario is not null)
        {
            WorldCoord? scenarioStart = ScenarioObjectiveBinder.ResolveStartCell(
                overworld.IslandPlan,
                scenario.StartLocation,
                overworld.Seed);
            if (scenarioStart is WorldCoord start &&
                IsGoodSpawnCell(overworld, start) &&
                CountPassableNeighbors(overworld, start) >= 3)
            {
                return start;
            }
        }

        WorldCoord preferred = new WorldCoord(overworld.Width / 2, overworld.Height / 2);
        if (overworld.IslandPlan is not null && overworld.IslandPlan.VisitorCenterCell.X >= 0)
        {
            WorldCoord visitor = overworld.IslandPlan.VisitorCenterCell;
            if (overworld.IslandPlan.IsLand(visitor.X, visitor.Y))
            {
                preferred = visitor;
            }
        }

        return FindBestSpawnCell(overworld, preferred);
    }

    private static WorldCoord FindBestSpawnCell(Overworld overworld, WorldCoord preferred)
    {
        if (IsGoodSpawnCell(overworld, preferred) && CountPassableNeighbors(overworld, preferred) >= 3)
        {
            return preferred;
        }

        for (int radius = 1; radius <= 12; radius++)
        {
            WorldCoord? best = null;
            int bestPassableNeighbors = -1;

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius)
                    {
                        continue;
                    }

                    var coord = new WorldCoord(preferred.X + dx, preferred.Y + dy);
                    if (!IsGoodSpawnCell(overworld, coord))
                    {
                        continue;
                    }

                    int passableNeighbors = CountPassableNeighbors(overworld, coord);
                    if (passableNeighbors < 3)
                    {
                        continue;
                    }

                    if (passableNeighbors > bestPassableNeighbors)
                    {
                        bestPassableNeighbors = passableNeighbors;
                        best = coord;
                    }
                }
            }

            if (best is not null)
            {
                return best.Value;
            }
        }

        return FindInlandStart(overworld);
    }

    private static bool IsGoodSpawnCell(Overworld overworld, WorldCoord coord)
    {
        if (!overworld.Contains(coord))
        {
            return false;
        }

        if (overworld.IslandPlan is not null && !overworld.IslandPlan.IsLand(coord.X, coord.Y))
        {
            return false;
        }

        BiomeId biome = overworld.GetCellValue(coord).Biome;
        return BiomeTraversal.IsPassable(biome);
    }

    private static int CountPassableNeighbors(Overworld overworld, WorldCoord coord)
    {
        int count = 0;
        (int dx, int dy)[] deltas = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (1, 1), (-1, 1), (-1, -1)];
        foreach ((int dx, int dy) in deltas)
        {
            var neighbor = new WorldCoord(coord.X + dx, coord.Y + dy);
            if (!overworld.Contains(neighbor))
            {
                continue;
            }

            if (BiomeTraversal.IsPassable(overworld.GetCellValue(neighbor).Biome))
            {
                count++;
            }
        }

        return count;
    }

    private static WorldCoord FindInlandStart(Overworld overworld)
    {
        int centerX = overworld.Width / 2;
        int centerY = overworld.Height / 2;

        for (int radius = 0; radius < Math.Max(overworld.Width, overworld.Height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    if (!overworld.Contains(new WorldCoord(x, y)))
                    {
                        continue;
                    }

                    if (overworld.IslandPlan?.IsLand(x, y) == true &&
                        BiomeTraversal.IsPassable(overworld.GetCellValue(new WorldCoord(x, y)).Biome))
                    {
                        return new WorldCoord(x, y);
                    }
                }
            }
        }

        return new WorldCoord(centerX, centerY);
    }

    public Overworld Overworld { get; }
    public ILocalMapRepository LocalMapRepository { get; }
    public GameViewMode ViewMode { get; set; }
    public WorldCoord PlayerWorldPosition { get; set; }
    public LocalCoord PlayerLocalPosition { get; set; }
    public Direction PlayerFacing { get; set; } = Direction.South;

    public void UpdateFacingFromDelta(int deltaX, int deltaY)
    {
        if (DirectionResolver.TryFromDelta(deltaX, deltaY, out Direction facing))
        {
            PlayerFacing = facing;
        }
    }
    public LocalMap? ActiveLocalMap { get; set; }
    public MapKey? SurfaceReturnKey { get; set; }
    public LocalCoord? SurfaceReturnPosition { get; set; }
    public bool IsInStructureInterior => ActiveLocalMap?.IsStructureInterior == true;
    public Inventory Inventory { get; }
    public QuestLog QuestLog { get; }
    public CharacterProgress CharacterProgress { get; }
    public MessageLog MessageLog { get; } = new();
    public IslandPressureClock PressureClock { get; } = new();

    public IslandPressureState PressureState { get; } = new();
    public RunScenario? RunScenario { get; set; }
    public bool FirstEncounterTriggered { get; set; }
    public FinaleThreatMemory FinaleThreats { get; } = new();
    public RunOutcome Outcome { get; private set; }
    public EscapeEndingKind EscapeEnding { get; private set; }
    public string? RunEndTitle { get; private set; }
    public string? RunEndSummary { get; private set; }
    public bool IsRunComplete => Outcome != RunOutcome.None;
    public bool RenderDirty { get; private set; } = true;
    public bool VisibilityDirty { get; private set; } = true;

    public bool HasQueuedMovement => _movementPath.Count > 0;
    internal Queue<(int X, int Y)> MovementPath => _movementPath;
    internal bool EnterOnArrival { get; set; }
    internal bool TransitionOnArrival { get; set; }
    internal Direction TransitionEdgeOnArrival { get; set; }
    internal int TransitionBorderX { get; set; }
    internal int TransitionBorderY { get; set; }

    public bool MovementEnterOnArrival => EnterOnArrival;
    public bool MovementTransitionOnArrival => TransitionOnArrival;
    public int MovementTransitionEdge => (int)TransitionEdgeOnArrival;
    public int MovementTransitionBorderX => TransitionBorderX;
    public int MovementTransitionBorderY => TransitionBorderY;

    public EntityRegistry Entities => _entityRegistry;

    public Entity PlayerEntity
    {
        get
        {
            if (ActiveLocalMap is not null)
            {
                Entity? player = ActiveLocalMap.Entities.GetById(EntityId.Player);
                if (player is not null)
                {
                    return player;
                }
            }

            return EntityFactory.CreatePlayer(
                PlayerWorldPosition,
                PlayerLocalPosition,
                PlayerTurnState,
                PlayerHealth,
                PlayerMaxHealth);
        }
    }

    public ActorTurnState PlayerTurnState { get; } = new();
    public int PlayerHealth { get; set; } = 100;
    public int PlayerMaxHealth { get; set; } = 100;
    public long WorldTime { get; set; }

    public bool[] VisibleTiles => _visibleTiles;

    public void MarkRenderDirty() => RenderDirty = true;

    public void MarkVisibilityDirty() => VisibilityDirty = true;

    public void ClearRenderDirty() => RenderDirty = false;

    public void EnsurePlayerEntity()
    {
        if (ActiveLocalMap is null)
        {
            return;
        }

        Entity? existing = ActiveLocalMap.Entities.GetById(EntityId.Player);
        if (existing is null)
        {
            LocalCoord spawn = WalkabilityHelper.FindUnoccupiedWalkable(ActiveLocalMap, PlayerLocalPosition);
            for (int attempt = 0; attempt < LocalMap.Width * LocalMap.Height && ActiveLocalMap.Entities.GetAt(spawn) is not null; attempt++)
            {
                spawn = new LocalCoord((spawn.X + 1) % LocalMap.Width, (spawn.Y + 1) % LocalMap.Height);
                spawn = WalkabilityHelper.FindUnoccupiedWalkable(ActiveLocalMap, spawn);
            }

            PlayerLocalPosition = spawn;
            Entity player = EntityFactory.CreatePlayer(
                PlayerWorldPosition,
                PlayerLocalPosition,
                PlayerTurnState,
                PlayerHealth,
                PlayerMaxHealth);
            ActiveLocalMap.Entities.Add(player);
        }
        else
        {
            existing.Actor = PlayerTurnState;
            existing.Health = PlayerHealth;
            existing.MaxHealth = PlayerMaxHealth;
            SyncPlayerEntityPosition();
        }
    }

    public void RefreshPlayerVitals()
    {
        if (ActiveLocalMap?.Entities.GetById(EntityId.Player) is Entity player)
        {
            PlayerHealth = player.Health;
            PlayerMaxHealth = player.MaxHealth;
        }
    }

    public void SyncPlayerEntityPosition()
    {
        if (ActiveLocalMap is null)
        {
            return;
        }

        Entity? player = ActiveLocalMap.Entities.GetById(EntityId.Player);
        if (player is null)
        {
            return;
        }

        player.WorldPosition = PlayerWorldPosition;
        player.LocalPosition = PlayerLocalPosition;
    }

    public void EnterWorldCell(LocalCoord? entryPoint = null)
    {
        WorldCoord coordinate = PlayerWorldPosition;

        ClearMovement();
        ActiveLocalMap = LocalMapRepository.GetOrGenerateSurface(coordinate);

        LocalCoord intended;
        if (entryPoint is not null)
        {
            intended = entryPoint.Value;
        }
        else if (Overworld.IslandPlan is not null &&
                 OverworldLandmarkCatalog.TryResolveEntryPoint(Overworld.IslandPlan, coordinate, ActiveLocalMap, out LocalCoord landmarkEntry))
        {
            intended = landmarkEntry;
        }
        else
        {
            intended = new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2);
        }

        if (!WalkabilityHelper.TryFindNearestWalkable(ActiveLocalMap, intended, out LocalCoord landing))
        {
            IReadOnlyList<EdgeConnection> connections = Overworld.GetEdgeConnections(coordinate).ToArray();
            LocalCoord? roadEntry = WalkabilityHelper.FindRoadCorridorEntry(ActiveLocalMap, connections);
            if (roadEntry is not null)
            {
                landing = WalkabilityHelper.FindNearestWalkable(ActiveLocalMap, roadEntry.Value);
            }
            else
            {
                landing = WalkabilityHelper.FindNearestWalkable(ActiveLocalMap, intended);
            }
        }

        PlayerLocalPosition = WalkabilityHelper.FindUnoccupiedWalkable(ActiveLocalMap, landing);
        ViewMode = GameViewMode.LocalMap;

        EnsurePlayerEntity();
        TrySpawnPendingPressurePredator();
        ScenarioEncounterResolver.TryTriggerFirstEncounter(this);
        UpdateVisibility();
        MarkRenderDirty();
        MessageLog.Add($"Entered local map at {coordinate.X}, {coordinate.Y}.");
        ScenarioObjectiveTracker.Check(this);
    }

    public bool CanLeaveLocalMap() => CanLeaveLocalMap(out _);

    public bool CanLeaveLocalMap(out string? blockedReason)
    {
        blockedReason = null;

        if (ViewMode != GameViewMode.LocalMap || ActiveLocalMap is null)
        {
            return false;
        }

        if (IsInStructureInterior)
        {
            blockedReason = "Exit the building before returning to the overworld.";
            return false;
        }

        if (HasAdjacentHostile())
        {
            blockedReason = "Too dangerous to leave the area right now.";
            return false;
        }

        return true;
    }

    public void LeaveLocalMap()
    {
        if (ActiveLocalMap is not null)
        {
            RefreshPlayerVitals();
            LocalMapRepository.Store(ActiveLocalMap);
            ref WorldCell cell = ref Overworld.GetCell(ActiveLocalMap.WorldPosition);
            cell.HasLocalChanges = true;
            ActiveLocalMap.Entities.Remove(EntityId.Player);
        }

        ActiveLocalMap = null;
        ViewMode = GameViewMode.Overworld;
        MarkVisibilityDirty();
        UpdateVisibility();
        MarkRenderDirty();
        ScenarioObjectiveTracker.Check(this);
    }

    public bool TryMoveOverworld(int deltaX, int deltaY)
    {
        bool moved = _movement.TryMoveOverworld(this, deltaX, deltaY);
        if (moved)
        {
            MarkRenderDirty();
            ScenarioObjectiveTracker.Check(this);
        }

        return moved;
    }

    public bool CanEnterOverworldCoord(WorldCoord coord)
    {
        if (PressureState.HazardousTravelCell == coord || ScenarioObstacleResolver.IsBlocked(this, coord))
        {
            return false;
        }

        return _movement.CanEnterOverworldCell(Overworld, coord);
    }

    public bool CanEnterOverworldStep(int deltaX, int deltaY)
    {
        var next = new WorldCoord(PlayerWorldPosition.X + deltaX, PlayerWorldPosition.Y + deltaY);
        return CanEnterOverworldCoord(next);
    }

    public int GetOverworldStepCost(int deltaX, int deltaY)
    {
        WorldCoord from = PlayerWorldPosition;
        var next = new WorldCoord(from.X + deltaX, from.Y + deltaY);
        return _movement.GetOverworldMoveCost(Overworld, from, next) + PressureState.TravelStaminaPenalty;
    }

    public int GetQueuedOverworldStepCost()
    {
        if (_movementPath.Count == 0)
        {
            return ActionCostTable.Walk + PressureState.TravelStaminaPenalty;
        }

        (int X, int Y) next = _movementPath.Peek();
        return _movement.GetOverworldMoveCost(Overworld, PlayerWorldPosition, new WorldCoord(next.X, next.Y))
            + PressureState.TravelStaminaPenalty;
    }

    public bool CanAffordQueuedStep()
    {
        if (_movementPath.Count == 0)
        {
            return false;
        }

        if (ViewMode == GameViewMode.Overworld)
        {
            return PlayerTurnState.Energy >= GetQueuedOverworldStepCost();
        }

        return true;
    }

    public bool CanIssuePlayerCommand() => !IsRunComplete;

    private bool HasAdjacentHostile()
    {
        if (ActiveLocalMap is null)
        {
            return false;
        }

        LocalCoord player = PlayerLocalPosition;
        foreach (Entity entity in ActiveLocalMap.Entities.All)
        {
            if (!entity.IsActive || entity.Id == EntityId.Player || !entity.IsAlive)
            {
                continue;
            }

            if (!FactionRelations.IsHostile(FactionId.Player, entity.Faction))
            {
                continue;
            }

            int deltaX = Math.Abs(entity.LocalPosition.X - player.X);
            int deltaY = Math.Abs(entity.LocalPosition.Y - player.Y);
            if (deltaX <= 1 && deltaY <= 1)
            {
                return true;
            }
        }

        return false;
    }

    public void CompleteRun(RunOutcome outcome, EscapeEndingKind escapeEnding, string title, string summary)
    {
        Outcome = outcome;
        EscapeEnding = escapeEnding;
        RunEndTitle = title;
        RunEndSummary = summary;
        ClearMovement();
        MarkRenderDirty();
    }

    public void RestoreRunState(RunOutcome outcome, EscapeEndingKind escapeEnding, string? title, string? summary)
    {
        Outcome = outcome;
        EscapeEnding = escapeEnding;
        RunEndTitle = title;
        RunEndSummary = summary;
    }

    public void RevealOverworldAroundPlayer()
    {
        if (ViewMode != GameViewMode.Overworld)
        {
            return;
        }

        OverworldExploration.RevealAround(Overworld, PlayerWorldPosition, OverworldExploration.RevealRadius);
        MarkRenderDirty();
    }

    public bool DebugRevealAll { get; private set; }

    public void RevealEntireOverworld()
    {
        OverworldExploration.RevealAll(Overworld);
        DebugRevealAll = true;
        UpdateVisibility();
        MarkRenderDirty();
    }

    public void AdvancePressureClock(int amount)
    {
        PressureClock.Advance(amount);
        while (PressureClock.TryConsumeEvent(out int threshold, out string message))
        {
            MessageLog.Add(message);
            PressureEventResolver.Apply(this, threshold);
            MarkRenderDirty();
        }

        ScenarioObjectiveTracker.Check(this);
    }

    public void NotifyWorldHourElapsed()
    {
        if (PressureState.EvacHoursRemaining is not int remaining)
        {
            return;
        }

        remaining--;
        if (remaining <= 0)
        {
            PressureState.EvacHoursRemaining = 0;
            if (!PressureState.MissedEvacuation)
            {
                PressureState.MissedEvacuation = true;
                FinaleThreats.Record(FinaleThreatId.MissedEvacuation);
                MessageLog.Add("The evacuation window closed. You're on your own.");
                MarkRenderDirty();
            }

            return;
        }

        PressureState.EvacHoursRemaining = remaining;
        if (remaining is 12 or 6)
        {
            MessageLog.Add($"Evacuation closes in {remaining} hours.");
            MarkRenderDirty();
        }
    }

    public void TrySpawnPendingPressurePredator()
    {
        if (!PressureState.PendingPredatorSpawn)
        {
            return;
        }

        PressureState.PendingPredatorSpawn = false;
        if (EntityFactory.TrySpawnPressurePredator(this))
        {
            MessageLog.Add("Something large moves through the trees nearby!");
            MarkRenderDirty();
        }
    }

    public bool CanUseTileTransition(
        int x,
        int y,
        StructureBlueprintCatalog blueprintCatalog,
        TileTransitionKind? preferredKind = null)
    {
        return _structureTransitions.CanUseTransition(this, x, y, blueprintCatalog, out _, preferredKind);
    }

    public bool CanUseTileTransition(
        int x,
        int y,
        StructureBlueprintCatalog blueprintCatalog,
        out TileTransition transition,
        TileTransitionKind? preferredKind = null)
    {
        return _structureTransitions.CanUseTransition(this, x, y, blueprintCatalog, out transition, preferredKind);
    }

    public bool TryUseTileTransition(
        int x,
        int y,
        StructureBlueprintCatalog blueprintCatalog,
        TileTransitionKind? preferredKind = null)
    {
        if (!_structureTransitions.CanUseTransition(this, x, y, blueprintCatalog, out TileTransition transition, preferredKind))
        {
            if (ActiveLocalMap is not null &&
                TileTransitionResolver.TryResolveAt(
                    Overworld,
                    ActiveLocalMap,
                    new LocalCoord(x, y),
                    blueprintCatalog,
                    out TileTransition blocked) &&
                blocked.RequiresRope)
            {
                MessageLog.Add("You need rope to descend from here.");
                MarkRenderDirty();
            }

            return false;
        }

        bool moved = _structureTransitions.TryApplyTransition(this, transition, blueprintCatalog);
        if (moved)
        {
            TrySpawnPendingPressurePredator();
            ScenarioObjectiveTracker.Check(this);
        }

        return moved;
    }

    public bool CanEnterStructureAt(int x, int y, StructureBlueprintCatalog blueprintCatalog)
    {
        return CanUseTileTransition(x, y, blueprintCatalog, out TileTransition transition) &&
               transition.Kind == TileTransitionKind.EnterStructure;
    }

    public bool CanExitStructureAt(int x, int y, StructureBlueprintCatalog blueprintCatalog)
    {
        return CanUseTileTransition(x, y, blueprintCatalog, out TileTransition transition) &&
               transition.Kind == TileTransitionKind.ExitStructure;
    }

    public bool CanUseStairsAt(int x, int y, StructureBlueprintCatalog blueprintCatalog)
    {
        return CanUseTileTransition(x, y, blueprintCatalog, out TileTransition up, TileTransitionKind.StairsUp) ||
               CanUseTileTransition(x, y, blueprintCatalog, out TileTransition down, TileTransitionKind.StairsDown);
    }

    public bool CanUseRopeAt(int x, int y, StructureBlueprintCatalog blueprintCatalog)
    {
        return CanUseTileTransition(x, y, blueprintCatalog, out TileTransition transition) &&
               transition.Kind == TileTransitionKind.RopeDescent;
    }

    public bool TryMoveLocal(int deltaX, int deltaY)
    {
        bool moved = _movement.TryMoveLocal(this, deltaX, deltaY);
        if (moved)
        {
            MarkRenderDirty();
        }

        return moved;
    }

    public bool TryTransitionAcrossEdge(Direction edge, int borderLocalX, int borderLocalY)
    {
        bool moved = _transitions.TryTransitionAcrossEdge(this, edge, borderLocalX, borderLocalY);
        if (moved)
        {
            MarkRenderDirty();
            MessageLog.Add($"Crossed border to {PlayerWorldPosition.X}, {PlayerWorldPosition.Y}.");
        }

        return moved;
    }

    public bool WouldTransitionAcrossEdge(Direction edge, int borderLocalX, int borderLocalY)
    {
        return _transitions.WouldTransitionAcrossEdge(this, edge, borderLocalX, borderLocalY);
    }

    public bool CanTransitionAcrossEdge(Direction edge, int borderLocalX, int borderLocalY)
    {
        return _transitions.CanTransitionAcrossEdge(this, edge, borderLocalX, borderLocalY);
    }

    public bool TryRemoveTerrainAtPlayer()
    {
        return TryRemoveTerrainAt(PlayerLocalPosition.X, PlayerLocalPosition.Y);
    }

    public bool TryRemoveTerrainAt(int x, int y)
    {
        if (ViewMode != GameViewMode.LocalMap || ActiveLocalMap is null)
        {
            return false;
        }

        if (!ActiveLocalMap.Contains(new LocalCoord(x, y)))
        {
            return false;
        }

        int index = ActiveLocalMap.GetIndex(x, y);
        if (ActiveLocalMap.Terrain[index] != TerrainId.Tree)
        {
            return false;
        }

        ActiveLocalMap.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
        MarkRenderDirty();
        return true;
    }

    public bool CanHarvestAt(int x, int y)
    {
        if (ViewMode != GameViewMode.LocalMap || ActiveLocalMap is null)
        {
            return false;
        }

        var coord = new LocalCoord(x, y);
        if (!ActiveLocalMap.Contains(coord))
        {
            return false;
        }

        Entity? tree = ActiveLocalMap.Entities.GetAt(coord);
        return tree is not null && tree.Kind == EntityKind.HarvestableTree && tree.IsActive;
    }

    public bool CanRemoveTreeTerrainAt(int x, int y)
    {
        if (ViewMode != GameViewMode.LocalMap || ActiveLocalMap is null)
        {
            return false;
        }

        if (!ActiveLocalMap.Contains(new LocalCoord(x, y)))
        {
            return false;
        }

        int index = ActiveLocalMap.GetIndex(x, y);
        return ActiveLocalMap.Terrain[index] == TerrainId.Tree;
    }

    public bool CanEnterOverworldTile(int x, int y)
    {
        if (ViewMode != GameViewMode.Overworld)
        {
            return false;
        }

        var coord = new WorldCoord(x, y);
        return Overworld.Contains(coord) && CanEnterOverworldCoord(coord);
    }

    public bool TryHarvestAt(int x, int y)
    {
        bool harvested = _interactions.TryHarvest(this, x, y);
        if (harvested)
        {
            MarkRenderDirty();
        }

        return harvested;
    }

    public void ClearMovement()
    {
        _movementPath.Clear();
        EnterOnArrival = false;
        TransitionOnArrival = false;
    }

    public List<(int X, int Y)> SnapshotMovementPath() => _movementPath.ToList();

    public void RestoreMovementPath(
        IReadOnlyList<(int X, int Y)> path,
        bool enterOnArrival,
        bool transitionOnArrival,
        Direction transitionEdge,
        int transitionBorderX,
        int transitionBorderY)
    {
        ClearMovement();
        foreach ((int X, int Y) step in path)
        {
            _movementPath.Enqueue(step);
        }

        EnterOnArrival = enterOnArrival;
        TransitionOnArrival = transitionOnArrival;
        TransitionEdgeOnArrival = transitionEdge;
        TransitionBorderX = transitionBorderX;
        TransitionBorderY = transitionBorderY;
    }

    public bool QueueMoveToBorderTransition(int targetX, int targetY, Direction edge)
    {
        if (ViewMode != GameViewMode.LocalMap)
        {
            return false;
        }

        if (!CanTransitionAcrossEdge(edge, targetX, targetY))
        {
            return false;
        }

        if (IsPlayerAt(targetX, targetY))
        {
            return TryTransitionAcrossEdge(edge, targetX, targetY);
        }

        ClearMovement();

        List<(int X, int Y)> path = _movement.BuildPathTo(this, targetX, targetY);
        if (path.Count == 0)
        {
            return false;
        }

        foreach ((int X, int Y) step in path)
        {
            _movementPath.Enqueue(step);
        }

        TransitionOnArrival = true;
        TransitionEdgeOnArrival = edge;
        TransitionBorderX = targetX;
        TransitionBorderY = targetY;
        return true;
    }

    public bool QueueMoveTo(int targetX, int targetY, bool enterOnArrival = false)
    {
        ClearMovement();

        List<(int X, int Y)> path = _movement.BuildPathTo(this, targetX, targetY);
        if (path.Count == 0)
        {
            if (enterOnArrival && IsPlayerAt(targetX, targetY) && ViewMode == GameViewMode.Overworld)
            {
                EnterWorldCell();
            }

            return false;
        }

        foreach ((int X, int Y) step in path)
        {
            _movementPath.Enqueue(step);
        }

        EnterOnArrival = enterOnArrival;
        return true;
    }

    public void AdvanceMovement()
    {
        _movement.AdvanceMovement(this);
        ScenarioObjectiveTracker.Check(this);
    }

    public void UpdateVisibility()
    {
        if (ViewMode == GameViewMode.LocalMap && ActiveLocalMap is not null)
        {
            EnsureVisibleBufferSize(ActiveLocalMap.Terrain.Length);
            FovCalculator.Compute(ActiveLocalMap, PlayerLocalPosition, _visibleTiles);
        }
        else if (ViewMode == GameViewMode.Overworld)
        {
            EnsureVisibleBufferSize(Overworld.Width * Overworld.Height);
            OverworldExploration.ComputeVisible(Overworld, PlayerWorldPosition, _visibleTiles);
        }
        else
        {
            VisibilityDirty = false;
            return;
        }

        VisibilityDirty = false;
        MarkRenderDirty();
    }

    private void EnsureVisibleBufferSize(int size)
    {
        if (_visibleTiles.Length != size)
        {
            _visibleTiles = new bool[size];
        }
    }

    public string InspectTile(int x, int y)
    {
        if (ViewMode == GameViewMode.Overworld)
        {
            if (!Overworld.Contains(new WorldCoord(x, y)))
            {
                return "Out of bounds.";
            }

            WorldCell cell = Overworld.GetCellValue(new WorldCoord(x, y));
            string geologyText = DescribeOverworldGeology(x, y);
            string travelText = DescribeOverworldTravelCost(x, y);
            return $"Overworld ({x}, {y}) biome={cell.Biome}{geologyText}{travelText} elev={cell.Elevation:F2}";
        }

        if (ActiveLocalMap is null || !ActiveLocalMap.Contains(new LocalCoord(x, y)))
        {
            return "Out of bounds.";
        }

        var coord = new LocalCoord(x, y);
        int index = ActiveLocalMap.GetIndex(x, y);
        Entity? entity = ActiveLocalMap.Entities.GetAt(coord);
        string entityText = entity is null
            ? "none"
            : entity.Kind == EntityKind.Raptor
                ? RaptorBehavior.Describe(entity)
                : entity.Kind.ToString();
        return $"Local ({x}, {y}) terrain={ActiveLocalMap.Terrain[index]} entity={entityText}";
    }

    private string DescribeOverworldLandmark(int x, int y)
    {
        if (Overworld.IslandPlan is null || !Overworld.Explored[Overworld.GetIndex(new WorldCoord(x, y))])
        {
            return string.Empty;
        }

        return OverworldLandmarkCatalog.GetName(Overworld.IslandPlan, x, y);
    }

    private string DescribeOverworldGeology(int x, int y)
    {
        if (Overworld.IslandPlan is null || !Overworld.Explored[Overworld.GetIndex(new WorldCoord(x, y))])
        {
            return string.Empty;
        }

        var parts = new List<string>();
        PlateBoundaryType boundary = Overworld.IslandPlan.GetCell(x, y).BoundaryType;
        if (boundary != PlateBoundaryType.None)
        {
            parts.Add($"boundary={boundary}");
        }

        ConnectionFlags flags = Overworld.GetCellValue(new WorldCoord(x, y)).ConnectionFlags;
        if ((flags & (ConnectionFlags.NorthRiver | ConnectionFlags.EastRiver | ConnectionFlags.SouthRiver | ConnectionFlags.WestRiver)) != 0)
        {
            parts.Add("river-ford");
        }

        return parts.Count == 0 ? string.Empty : $" {string.Join(' ', parts)}";
    }

    private string DescribeOverworldTravelCost(int x, int y)
    {
        if (ViewMode != GameViewMode.Overworld)
        {
            return string.Empty;
        }

        int dx = x - PlayerWorldPosition.X;
        int dy = y - PlayerWorldPosition.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1 || !CanEnterOverworldCoord(new WorldCoord(x, y)))
        {
            return string.Empty;
        }

        int cost = GetOverworldStepCost(dx, dy);
        return $" step={cost} stamina";
    }

    private bool IsPlayerAt(int x, int y)
    {
        return ViewMode == GameViewMode.Overworld
            ? PlayerWorldPosition.X == x && PlayerWorldPosition.Y == y
            : PlayerLocalPosition.X == x && PlayerLocalPosition.Y == y;
    }
}
