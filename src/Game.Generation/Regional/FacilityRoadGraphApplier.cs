using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Regional;

public static class FacilityRoadGraphApplier
{
    public static void ApplyToOverworld(Overworld world, IslandPlan plan, int roadWidth = 2)
    {
        var seenPairs = new HashSet<(int Ax, int Ay, int Bx, int By)>();

        foreach ((int x, int y) in plan.RoadGraph.PathCells)
        {
            var coord = new WorldCoord(x, y);
            foreach ((int dx, int dy, Direction edge) in new (int, int, Direction)[]
                     {
                         (1, 0, Direction.East),
                         (0, 1, Direction.South),
                     })
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.RoadGraph.PathCells.Contains((nx, ny)))
                {
                    continue;
                }

                (int ax, int ay, int bx, int by) key = x < nx || (x == nx && y < ny)
                    ? (x, y, nx, ny)
                    : (nx, ny, x, y);
                if (!seenPairs.Add(key))
                {
                    continue;
                }

                var fromCoord = new WorldCoord(x, y);
                var toCoord = new WorldCoord(nx, ny);
                if (!GlobalTilePathUtility.TryComputeEdgeLocalOffset(
                        plan.RoadGraph.GlobalPathTiles,
                        fromCoord,
                        toCoord,
                        edge,
                        roadWidth,
                        out int localOffset))
                {
                    continue;
                }

                world.AddEdgeConnection(
                    fromCoord,
                    new EdgeConnection(edge, localOffset, ConnectionType.Road, roadWidth));

                world.AddEdgeConnection(
                    toCoord,
                    new EdgeConnection(edge.Opposite(), localOffset, ConnectionType.Road, roadWidth));
            }
        }
    }
}
