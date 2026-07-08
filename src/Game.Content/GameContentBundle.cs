using Game.Content.Definitions;
using Game.Simulation.World.Island;

namespace Game.Content;

public sealed class GameContentBundle
{
    public required ControlsDefinition Controls { get; init; }
    public required BiomeColorsDefinition BiomeColors { get; init; }
    public required TerrainColorsDefinition TerrainColors { get; init; }
    public required TilesDefinition Tiles { get; init; }
    public required CameraDefinition Camera { get; init; }
    public required PlayerDefinition Player { get; init; }
    public required BiomeRulesDefinition BiomeRules { get; init; }
    public required IslandDefinition Island { get; init; }
    public required ContextMenusDefinition ContextMenus { get; init; }
    public required QuestsDefinition Quests { get; init; }
    public required ItemsDefinition Items { get; init; }
    public required CharacterDefaultsDefinition CharacterDefaults { get; init; }
    public required UiThemeDefinition UiTheme { get; init; }
    public required StructureBlueprintsDefinition StructureBlueprints { get; init; }

    public StructureBlueprintCatalog CreateBlueprintCatalog() => new(StructureBlueprints);
}
