namespace Game.Generation.Regional;

public static class GlobalTileConnectivityValidator
{
    private static readonly (int Dx, int Dy)[] Neighbors =
        [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static bool IsConnected(IReadOnlySet<(int GlobalX, int GlobalY)> tiles)
    {
        return CountConnectedTiles(tiles) == tiles.Count;
    }

    public static int CountConnectedTiles(IReadOnlySet<(int GlobalX, int GlobalY)> tiles)
    {
        if (tiles.Count == 0)
        {
            return 0;
        }

        var visited = new HashSet<(int GlobalX, int GlobalY)>();
        var queue = new Queue<(int GlobalX, int GlobalY)>();
        (int GlobalX, int GlobalY) first = tiles.First();
        visited.Add(first);
        queue.Enqueue(first);

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            foreach ((int dx, int dy) in Neighbors)
            {
                var neighbor = (GlobalX: x + dx, GlobalY: y + dy);
                if (tiles.Contains(neighbor) && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return visited.Count;
    }
}
