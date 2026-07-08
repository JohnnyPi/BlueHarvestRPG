using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Rendering;

public readonly record struct EntityRenderData(int X, int Y, int Kind, Direction Facing, string? SpriteId);

public sealed record RenderSnapshot(
    string Title,
    GameViewMode ViewMode,
    int GridWidth,
    int GridHeight,
    ushort[] CellData,
    int PlayerX,
    int PlayerY,
    Direction PlayerFacing,
    string DebugInfo,
    int TickCount,
    EntityRenderData[] Entities,
    bool[]? VisibleTiles,
    bool[]? ExploredTiles,
    string[] MessageLog,
    string? HoverTooltip,
    PlayerStatusView PlayerStatus,
    InventoryItemView[] InventoryItems,
    QuestItemView[] QuestItems,
    CharacterSheetView CharacterSheet,
    long WorldTime = 0,
    int PlayerEnergy = 100,
    bool WaitingForPlayerInput = true,
    string? ScenarioMission = null,
    int IslandPressure = 0,
    int TravelStaminaPenalty = 0,
    int? EvacHoursRemaining = null,
    int? HazardousTravelX = null,
    int? HazardousTravelY = null,
    OverworldLandmarkView[]? OverworldLandmarks = null,
    byte[]? TectonicBoundaries = null,
    byte[]? RiverEdgeMask = null,
    RunOutcome RunOutcome = RunOutcome.None,
    EscapeEndingKind EscapeEnding = EscapeEndingKind.None,
    string? RunEndTitle = null,
    string? RunEndSummary = null,
    bool CanReturnToOverworld = false);
