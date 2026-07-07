namespace Game.Simulation.World.Island;

public static class StructureBlueprintCatalogDefaults
{
    public static StructureBlueprintCatalog Create()
    {
        return new StructureBlueprintCatalog(new StructureBlueprintsDefinition
        {
            Blueprints =
            [
                Blueprint("visitor_center", ["VisitorCenter"], 1, 1, 20, 10, 14, 23, detail: "Counter"),
                Blueprint("hotel", ["Hotel"], 3, 0, 20, 8, 12, 17, ropeFloor: 2, ropeX: 22, ropeY: 1, detail: "Counter"),
                Blueprint("restaurant", ["Restaurant"], 1, 0, 6, 5, 7, 11, detail: "Counter"),
                Blueprint("maintenance", ["MaintenanceCompound"], 1, 1, 5, 4, 6, 9, detail: "Machinery"),
                Blueprint("attraction", ["Attraction"], 1, 0, 8, 8, 9, 17, detail: "Rubble"),
                Blueprint("dock", ["Dock"], 1, 0, 10, 5, 10, 11),
                Blueprint("helipad", ["Helipad"], 1, 0, 5, 5, 5, 5),
                Blueprint("default_building", [], 1, 0, 4, 4, 4, 8)
            ]
        });
    }

    private static StructureBlueprintDefinition Blueprint(
        string id,
        string[] types,
        int floorCount,
        int basementCount,
        int stairX,
        int stairY,
        int doorX,
        int doorY,
        int? ropeFloor = null,
        int? ropeX = null,
        int? ropeY = null,
        string detail = "")
    {
        return new StructureBlueprintDefinition
        {
            Id = id,
            StructureTypes = types.ToList(),
            FloorCount = floorCount,
            BasementCount = basementCount,
            StairX = stairX,
            StairY = stairY,
            DoorX = doorX,
            DoorY = doorY,
            RopeExitFloor = ropeFloor,
            RopeExitX = ropeX,
            RopeExitY = ropeY,
            DetailTerrain = detail
        };
    }
}
