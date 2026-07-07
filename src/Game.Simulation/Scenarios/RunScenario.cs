using Game.Simulation.Coordinates;

namespace Game.Simulation.Scenarios;

public sealed class RunScenario
{
    public required string Mission { get; init; }
    public required string StartLocation { get; init; }
    public required string EscapeRoute { get; init; }
    public required string Obstacle1 { get; init; }
    public required string Obstacle2 { get; init; }
    public required string Mystery { get; init; }
    public required string FirstEncounter { get; init; }
    public required string IslandSecret { get; init; }

    public WorldCoord? EscapeTarget { get; set; }
    public WorldCoord? MysteryTarget { get; set; }
    public WorldCoord? Obstacle1Target { get; set; }
    public WorldCoord? Obstacle2Target { get; set; }
    public string EscapeLandmark { get; set; } = string.Empty;
    public string MysteryLandmark { get; set; } = string.Empty;
}
