using Game.Simulation.World.Island;
using Game.Content.Definitions;
using Game.Generation.Passes;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.LocalMaps;

public sealed class StructureFloorGenerator
{
    private readonly StructureBlueprintCatalog _blueprintCatalog;

    public StructureFloorGenerator(StructureBlueprintCatalog blueprintCatalog)
    {
        _blueprintCatalog = blueprintCatalog;
    }

    public LocalMap Generate(
        Overworld world,
        MapKey key,
        StructurePlacement structure)
    {
        if (!structure.HasFloor(key.FloorIndex))
        {
            throw new InvalidOperationException(
                $"Structure {structure.InstanceId} does not have floor {key.FloorIndex}.");
        }

        var map = new LocalMap(key);
        FillBase(map);

        StructureBlueprintDefinition blueprint = _blueprintCatalog.ResolveById(structure.BlueprintId);
        StructureStampHelper.StampBuilding(
            map,
            key.WorldPosition,
            structure,
            blueprint,
            key.FloorIndex,
            surfaceView: false);

        return map;
    }

    private static void FillBase(LocalMap map)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
            }
        }
    }

    public static StructurePlacement? FindStructure(IslandPlan plan, int instanceId) =>
        StructurePlacementQueries.FindByInstanceId(plan, instanceId);

    public static StructurePlacement? FindStructureAt(
        IslandPlan plan,
        WorldCoord worldCell,
        LocalCoord localPosition) =>
        StructurePlacementQueries.FindAtLocalPosition(plan, worldCell, localPosition);

    public static ulong DeriveFloorSeed(ulong worldSeed, int structureInstanceId, int floorIndex)
    {
        return SeedUtility.Derive(worldSeed, structureInstanceId, floorIndex, WorldGeneratorVersion.Current);
    }
}
