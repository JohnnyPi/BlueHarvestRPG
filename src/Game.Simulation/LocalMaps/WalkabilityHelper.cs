using Game.Simulation.Coordinates;
using Game.Simulation.World;

namespace Game.Simulation.LocalMaps;

public static class WalkabilityHelper
{
    public static bool TryFindNearestWalkable(LocalMap map, LocalCoord intended, out LocalCoord result)
    {
        if (map.Contains(intended) && !map.BlocksMovement(intended))
        {
            result = intended;
            return true;
        }

        var start = map.Contains(intended)
            ? intended
            : new LocalCoord(
                Math.Clamp(intended.X, 0, LocalMap.Width - 1),
                Math.Clamp(intended.Y, 0, LocalMap.Height - 1));

        var queue = new Queue<LocalCoord>();
        var visited = new bool[LocalMap.Width * LocalMap.Height];
        queue.Enqueue(start);
        visited[map.GetIndex(start.X, start.Y)] = true;

        while (queue.Count > 0)
        {
            LocalCoord current = queue.Dequeue();

            if (!map.BlocksMovement(current))
            {
                result = current;
                return true;
            }

            foreach ((int dx, int dy) in GridDirections.Cardinal)
            {
                int nx = current.X + dx;
                int ny = current.Y + dy;
                if (nx < 0 || ny < 0 || nx >= LocalMap.Width || ny >= LocalMap.Height)
                {
                    continue;
                }

                int index = ny * LocalMap.Width + nx;
                if (visited[index])
                {
                    continue;
                }

                visited[index] = true;
                queue.Enqueue(new LocalCoord(nx, ny));
            }
        }

        result = default;
        return false;
    }

    public static LocalCoord FindNearestWalkable(LocalMap map, LocalCoord intended)
    {
        if (TryFindNearestWalkable(map, intended, out LocalCoord result))
        {
            return result;
        }

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                var coord = new LocalCoord(x, y);
                if (!map.BlocksMovement(coord))
                {
                    return coord;
                }
            }
        }

        return map.Contains(intended)
            ? intended
            : new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2);
    }

    public static LocalCoord FindUnoccupiedWalkable(LocalMap map, LocalCoord preferred)
    {
        if (IsUnoccupiedWalkable(map, preferred))
        {
            return preferred;
        }

        if (TryFindNearestWalkable(map, preferred, out LocalCoord walkable))
        {
            var queue = new Queue<LocalCoord>();
            var visited = new bool[LocalMap.Width * LocalMap.Height];
            queue.Enqueue(walkable);
            visited[map.GetIndex(walkable.X, walkable.Y)] = true;

            while (queue.Count > 0)
            {
                LocalCoord current = queue.Dequeue();
                if (IsUnoccupiedWalkable(map, current))
                {
                    return current;
                }

                foreach ((int dx, int dy) in GridDirections.Cardinal)
                {
                    int nx = current.X + dx;
                    int ny = current.Y + dy;
                    if (nx < 0 || ny < 0 || nx >= LocalMap.Width || ny >= LocalMap.Height)
                    {
                        continue;
                    }

                    int index = ny * LocalMap.Width + nx;
                    if (visited[index])
                    {
                        continue;
                    }

                    visited[index] = true;
                    queue.Enqueue(new LocalCoord(nx, ny));
                }
            }
        }

        return FindFirstUnoccupiedWalkable(map) ?? FindNearestWalkable(map, preferred);
    }

    private static LocalCoord? FindFirstUnoccupiedWalkable(LocalMap map)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                var coord = new LocalCoord(x, y);
                if (IsUnoccupiedWalkable(map, coord))
                {
                    return coord;
                }
            }
        }

        return null;
    }

    private static bool IsUnoccupiedWalkable(LocalMap map, LocalCoord coord)
    {
        return map.Contains(coord) &&
               !map.BlocksMovement(coord) &&
               map.Entities.GetAt(coord) is null;
    }

    public static LocalCoord? FindRoadCorridorEntry(LocalMap map, IReadOnlyList<EdgeConnection> connections)
    {
        foreach (EdgeConnection connection in connections)
        {
            if (connection.Type != ConnectionType.Road)
            {
                continue;
            }

            if (connection.Edge is Direction.East or Direction.West)
            {
                for (int y = connection.LocalOffset; y < connection.LocalOffset + connection.Width; y++)
                {
                    var coord = new LocalCoord(0, y);
                    if (map.Contains(coord) && !map.BlocksMovement(coord))
                    {
                        return coord;
                    }
                }
            }
            else
            {
                for (int x = connection.LocalOffset; x < connection.LocalOffset + connection.Width; x++)
                {
                    var coord = new LocalCoord(x, 0);
                    if (map.Contains(coord) && !map.BlocksMovement(coord))
                    {
                        return coord;
                    }
                }
            }
        }

        return null;
    }
}

internal static class GridDirections
{
    public static readonly (int dx, int dy)[] Cardinal =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
    ];
}
