using Game.Simulation.Coordinates;

namespace Game.Simulation.World.Island;

public sealed class LavaFlow
{
    public List<WorldCoord> Path { get; } = [];
}

public sealed class LavaFlowGraph
{
    public List<LavaFlow> Flows { get; } = [];
    public HashSet<(int X, int Y)> PathCells { get; } = [];
    public HashSet<(int GlobalX, int GlobalY)> GlobalLavaTiles { get; } = [];
    public float RoadTraversalPenalty { get; set; } = 40f;

    public void Clear()
    {
        Flows.Clear();
        PathCells.Clear();
        GlobalLavaTiles.Clear();
    }

    public void AddPath(IEnumerable<WorldCoord> path)
    {
        foreach (WorldCoord cell in path)
        {
            PathCells.Add((cell.X, cell.Y));
        }
    }
}
