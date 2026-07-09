using Game.Simulation.Quests;

namespace Game.Simulation.Rendering;

public readonly record struct PlayerStatusView(
    int Health,
    int MaxHealth,
    int Energy,
    int Speed,
    int WorldX,
    int WorldY,
    int? LocalX,
    int? LocalY,
    string LocationLabel,
    string TerrainOrBiome,
    string StealthStatus = "",
    string MovementModeLabel = "Walk");

public readonly record struct InventoryItemView(int ItemId, string DisplayName, int Count);

public readonly record struct QuestItemView(
    string Id,
    string Title,
    string Objective,
    int Progress,
    int Target,
    QuestState State);

public readonly record struct AttributeView(string Id, string DisplayName, int Value);

public readonly record struct CharacterSheetView(
    int Level,
    int Experience,
    string Faction,
    AttributeView[] Attributes,
    int InventoryStackCount,
    int InventoryTotalCount);

public sealed class RenderViewContent
{
    public IReadOnlyDictionary<string, (string Title, string Objective, int Target)> Quests { get; init; }
        = new Dictionary<string, (string, string, int)>();

    public IReadOnlyDictionary<int, string> ItemDisplayNames { get; init; }
        = new Dictionary<int, string>();

    public IReadOnlyList<(string Id, string DisplayName)> AttributeDefinitions { get; init; }
        = [];
}
