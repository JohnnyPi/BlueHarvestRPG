namespace Game.Simulation.World.Island;

public sealed class StructureBlueprintsDefinition
{
    public List<StructureBlueprintDefinition> Blueprints { get; set; } = [];
}

public sealed class StructureBlueprintDefinition
{
    public string Id { get; set; } = string.Empty;

    public List<string> StructureTypes { get; set; } = [];

    public int FloorCount { get; set; } = 1;

    public int BasementCount { get; set; }

    public int StairX { get; set; }

    public int StairY { get; set; }

    public int DoorX { get; set; }

    public int DoorY { get; set; }

    public int? RopeExitFloor { get; set; }

    public int? RopeExitX { get; set; }

    public int? RopeExitY { get; set; }

    public string DetailTerrain { get; set; } = string.Empty;
}
