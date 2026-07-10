using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.World.Island;

public sealed class FacilityRiverSegment
{
    public List<WorldCoord> Path { get; } = [];
}

public sealed class FacilityRiverGraph
{
    public List<FacilityRiverSegment> Segments { get; } = [];
    public HashSet<(int X, int Y)> PathCells { get; } = [];
    public HashSet<(int GlobalX, int GlobalY)> GlobalRiverTiles { get; } = [];

    public void AddPath(IEnumerable<WorldCoord> path)
    {
        foreach (WorldCoord cell in path)
        {
            PathCells.Add((cell.X, cell.Y));
        }
    }

    public void AddGlobalPath(IEnumerable<(int GlobalX, int GlobalY)> tiles)
    {
        foreach ((int globalX, int globalY) in tiles)
        {
            GlobalRiverTiles.Add((globalX, globalY));
            PathCells.Add((globalX / LocalMap.Width, globalY / LocalMap.Height));
        }
    }
}
