using Game.Simulation.LocalMaps;

namespace Game.Simulation.Rendering;

public enum OverworldLandmarkObjectiveKind
{
    None = 0,
    Escape = 1,
    Mystery = 2
}

public enum OverworldLandmarkKind
{
    Structure = 0,
    Ruin = 1,
    Volcanic = 2,
    Site = 3
}

public readonly record struct OverworldLandmarkView(
    int GlobalOriginX,
    int GlobalOriginY,
    int FootprintWidth,
    int FootprintHeight,
    string Name,
    OverworldLandmarkKind Kind,
    OverworldLandmarkObjectiveKind ObjectiveKind = OverworldLandmarkObjectiveKind.None)
{
    public int X => (GlobalOriginX + FootprintWidth / 2) / LocalMap.Width;
    public int Y => (GlobalOriginY + FootprintHeight / 2) / LocalMap.Height;
}
