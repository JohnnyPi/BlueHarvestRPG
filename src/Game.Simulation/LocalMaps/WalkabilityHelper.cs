using Game.Simulation.Coordinates;

namespace Game.Simulation.LocalMaps;

public static class WalkabilityHelper
{
    public static LocalCoord FindNearestWalkable(LocalMap map, LocalCoord intended)
    {
        if (map.Contains(intended) && !map.BlocksMovement(intended))
        {
            return intended;
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

        return start;
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
