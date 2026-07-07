using Game.Content.Definitions;
using Game.Simulation.Input;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Game.Content;

public sealed class ContentLoader
{
    private readonly string _root;
    private readonly IDeserializer _deserializer;

    public ContentLoader(string? overrideRoot = null)
    {
        _root = ContentPaths.ResolveRoot(overrideRoot);
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public GameContentBundle LoadAll()
    {
        var controls = Load<ControlsDefinition>("controls.yaml");
        var biomeColors = Load<BiomeColorsDefinition>(Path.Combine("presentation", "biomes.yaml"));
        var terrainColors = Load<TerrainColorsDefinition>(Path.Combine("presentation", "terrain.yaml"));
        var camera = Load<CameraDefinition>(Path.Combine("presentation", "camera.yaml"));
        var biomeRules = Load<BiomeRulesDefinition>(Path.Combine("generation", "biome_rules.yaml"));
        var island = Load<IslandDefinition>(Path.Combine("generation", "island.yaml"));
        var contextMenus = Load<ContextMenusDefinition>(Path.Combine("ui", "context_menus.yaml"));
        var quests = Load<QuestsDefinition>(Path.Combine("quests", "quests.yaml"));
        var items = Load<ItemsDefinition>("items.yaml");
        var characterDefaults = Load<CharacterDefaultsDefinition>(Path.Combine("character", "defaults.yaml"));
        var uiTheme = Load<UiThemeDefinition>(Path.Combine("ui", "theme.yaml"));
        var structureBlueprints = Load<StructureBlueprintsDefinition>(Path.Combine("structures", "blueprints.yaml"));

        var bundle = new GameContentBundle
        {
            Controls = controls,
            BiomeColors = biomeColors,
            TerrainColors = terrainColors,
            Camera = camera,
            BiomeRules = biomeRules,
            Island = island,
            ContextMenus = contextMenus,
            Quests = quests,
            Items = items,
            CharacterDefaults = characterDefaults,
            UiTheme = uiTheme,
            StructureBlueprints = structureBlueprints,
        };

        Validate(bundle);
        return bundle;
    }

    private T Load<T>(string relativePath)
        where T : new()
    {
        string fullPath = Path.Combine(_root, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new ContentLoadException($"Content file not found: {fullPath}");
        }

        try
        {
            string text = File.ReadAllText(fullPath);
            return _deserializer.Deserialize<T>(text) ?? new T();
        }
        catch (Exception ex) when (ex is not ContentLoadException)
        {
            throw new ContentLoadException($"Failed to parse content file: {fullPath}", ex);
        }
    }

    private static void Validate(GameContentBundle bundle)
    {
        foreach ((string action, ActionBinding binding) in bundle.Controls.Actions)
        {
            if (binding.Keyboard.Count == 0 && binding.Mouse.Count == 0 && string.IsNullOrEmpty(binding.MouseWheel))
            {
                throw new ContentLoadException($"Control action '{action}' has no bindings.");
            }
        }

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            if (!bundle.BiomeColors.Biomes.ContainsKey(biome.ToString()))
            {
                throw new ContentLoadException($"Missing biome color for '{biome}'.");
            }
        }

        foreach (TerrainId terrain in Enum.GetValues<TerrainId>())
        {
            if (!bundle.TerrainColors.Terrain.ContainsKey(terrain.ToString()))
            {
                throw new ContentLoadException($"Missing terrain color for '{terrain}'.");
            }
        }

        ValidateMenu(bundle.ContextMenus.Overworld, "overworld");
        ValidateMenu(bundle.ContextMenus.LocalMap, "localMap");
    }

    private static void ValidateMenu(IReadOnlyList<ContextMenuEntry> entries, string section)
    {
        foreach (ContextMenuEntry entry in entries)
        {
            if (!Enum.TryParse<GameIntent>(entry.Intent, out _))
            {
                throw new ContentLoadException(
                    $"Context menu '{section}' entry '{entry.Id}' references unknown intent '{entry.Intent}'.");
            }
        }
    }
}
