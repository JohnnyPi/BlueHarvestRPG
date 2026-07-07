using Game.Simulation.LocalMaps;

namespace Game.Simulation.World.Island;

public sealed class StructureBlueprintCatalog
{
    private readonly Dictionary<string, StructureBlueprintDefinition> _byId;
    private readonly Dictionary<StructureType, StructureBlueprintDefinition> _byType;

    public StructureBlueprintCatalog(StructureBlueprintsDefinition definition)
    {
        _byId = definition.Blueprints.ToDictionary(b => b.Id, StringComparer.OrdinalIgnoreCase);
        _byType = new Dictionary<StructureType, StructureBlueprintDefinition>();

        foreach (StructureBlueprintDefinition blueprint in definition.Blueprints)
        {
            foreach (string typeName in blueprint.StructureTypes)
            {
                if (Enum.TryParse(typeName, ignoreCase: true, out StructureType type))
                {
                    _byType[type] = blueprint;
                }
            }
        }
    }

    public StructureBlueprintDefinition Resolve(StructureType type)
    {
        if (_byType.TryGetValue(type, out StructureBlueprintDefinition? blueprint))
        {
            return blueprint;
        }

        return _byId["default_building"];
    }

    public StructureBlueprintDefinition ResolveById(string blueprintId)
    {
        if (_byId.TryGetValue(blueprintId, out StructureBlueprintDefinition? blueprint))
        {
            return blueprint;
        }

        return _byId["default_building"];
    }

    public bool TryGetRopeExit(
        StructurePlacement structure,
        int floorIndex,
        out int withinX,
        out int withinY)
    {
        withinX = 0;
        withinY = 0;

        StructureBlueprintDefinition blueprint = ResolveById(structure.BlueprintId);
        if (blueprint.RopeExitFloor != floorIndex ||
            blueprint.RopeExitX is not int ropeX ||
            blueprint.RopeExitY is not int ropeY)
        {
            return false;
        }

        withinX = ropeX;
        withinY = ropeY;
        return true;
    }

    public static TerrainId? ParseDetailTerrain(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse(value, ignoreCase: true, out TerrainId terrain) ? terrain : null;
    }
}
