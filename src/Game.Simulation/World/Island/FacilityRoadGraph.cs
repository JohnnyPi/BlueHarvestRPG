using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.World.Island;

public enum FacilityRoadNodeKind
{
    Hub,
    Ring,
    Junction,
    CoastConnector,
    FacilitySpur
}

public sealed class FacilityRoadNode
{
    public int Id { get; init; }
    public WorldCoord Cell { get; init; }
    public FacilityRoadNodeKind Kind { get; init; }
}

public sealed class FacilityRoadSegment
{
    public int FromNodeId { get; init; }
    public int ToNodeId { get; init; }
    public List<WorldCoord> Path { get; } = [];
}

public sealed class FacilityRoadGraph
{
    public List<FacilityRoadNode> Nodes { get; } = [];
    public List<FacilityRoadSegment> Segments { get; } = [];
    public HashSet<(int X, int Y)> PathCells { get; } = [];
    public HashSet<(int GlobalX, int GlobalY)> GlobalPathTiles { get; } = [];

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
            GlobalPathTiles.Add((globalX, globalY));
        }
    }

    public void AddSpur(WorldCoord fromCell, WorldCoord toCell)
    {
        PathCells.Add((fromCell.X, fromCell.Y));
        PathCells.Add((toCell.X, toCell.Y));
        AddGlobalPath(BuildGlobalSpur(fromCell, toCell));
    }

    public static List<(int GlobalX, int GlobalY)> BuildGlobalSpur(WorldCoord fromCell, WorldCoord toCell)
    {
        (int fromGx, int fromGy) = CellCenterGlobal(fromCell);
        (int toGx, int toGy) = CellCenterGlobal(toCell);

        var tiles = new List<(int GlobalX, int GlobalY)>();
        int x = fromGx;
        int y = fromGy;

        while (x != toGx)
        {
            tiles.Add((x, y));
            x += Math.Sign(toGx - x);
        }

        while (y != toGy)
        {
            tiles.Add((x, y));
            y += Math.Sign(toGy - y);
        }

        tiles.Add((toGx, toGy));
        return tiles;
    }

    public WorldCoord? FindNearestRoadCell(WorldCoord target)
    {
        if (PathCells.Count == 0)
        {
            return null;
        }

        WorldCoord? best = null;
        int bestDist = int.MaxValue;
        foreach ((int x, int y) in PathCells)
        {
            int dist = Math.Abs(x - target.X) + Math.Abs(y - target.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = new WorldCoord(x, y);
            }
        }

        return best;
    }

    public bool IsAdjacentToRoad(WorldCoord cell)
    {
        foreach ((int dx, int dy) in new (int, int)[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            if (PathCells.Contains((cell.X + dx, cell.Y + dy)))
            {
                return true;
            }
        }

        return false;
    }

    public static (int GlobalX, int GlobalY) CellCenterGlobal(WorldCoord cell)
    {
        return (
            cell.X * LocalMap.Width + LocalMap.Width / 2,
            cell.Y * LocalMap.Height + LocalMap.Height / 2);
    }
}
