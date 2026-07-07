namespace Game.Simulation.Rendering;

public enum OverworldLandmarkObjectiveKind
{
    None = 0,
    Escape = 1,
    Mystery = 2
}

public readonly record struct OverworldLandmarkView(
    int X,
    int Y,
    string Name,
    OverworldLandmarkObjectiveKind ObjectiveKind = OverworldLandmarkObjectiveKind.None);
