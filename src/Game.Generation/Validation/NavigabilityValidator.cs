using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Validation;

public sealed class NavigabilityValidator
{
    public const float MinReachabilityShare = 0.90f;

    public ValidationResult ValidateAndRepair(LocalMap map, LocalCoord entryPoint)
    {
        float reachability = MeasureReachability(map, entryPoint, out int reachable, out int walkable);
        if (map.Contains(entryPoint) && map.BlocksMovement(entryPoint))
        {
            RepairEntryPoint(map, entryPoint);
            reachability = MeasureReachability(map, entryPoint, out reachable, out walkable);
        }

        int repairAttempts = 0;
        while (reachability < MinReachabilityShare && repairAttempts < 6)
        {
            if (!RepairBlockingTiles(map, entryPoint, repairAttempts))
            {
                break;
            }

            reachability = MeasureReachability(map, entryPoint, out reachable, out walkable);
            repairAttempts++;
        }

        return new ValidationResult(reachability, reachable, walkable, repairAttempts);
    }

    public bool EnsureConnected(
        LocalMap map,
        LocalCoord start,
        LocalCoord target,
        bool allowInteriorWallRepair = false)
    {
        if (!map.Contains(start) || !map.Contains(target))
        {
            return false;
        }

        if (TryFindPath(map, start, target, allowRepairs: false, allowInteriorWallRepair, out _))
        {
            return true;
        }

        if (!TryFindPath(map, start, target, allowRepairs: true, allowInteriorWallRepair, out List<LocalCoord>? path))
        {
            return false;
        }

        foreach (LocalCoord coord in path!)
        {
            if (!map.BlocksMovement(coord))
            {
                continue;
            }

            TerrainId replacement = map.IsStructureInterior ? TerrainId.Floor : TerrainId.Grass;
            map.SetTerrain(coord.X, coord.Y, replacement, TileFlags.None);
        }

        return TryFindPath(map, start, target, allowRepairs: false, allowInteriorWallRepair, out _);
    }

    private static bool TryFindPath(
        LocalMap map,
        LocalCoord start,
        LocalCoord target,
        bool allowRepairs,
        bool allowInteriorWallRepair,
        out List<LocalCoord>? path)
    {
        int tileCount = LocalMap.Width * LocalMap.Height;
        var previous = new int[tileCount];
        Array.Fill(previous, -1);
        int startIndex = map.GetIndex(start.X, start.Y);
        int targetIndex = map.GetIndex(target.X, target.Y);
        previous[startIndex] = startIndex;

        var queue = new Queue<LocalCoord>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            LocalCoord current = queue.Dequeue();
            if (current == target)
            {
                break;
            }

            foreach ((int dx, int dy) in Cardinal)
            {
                var next = new LocalCoord(current.X + dx, current.Y + dy);
                if (!map.Contains(next))
                {
                    continue;
                }

                int nextIndex = map.GetIndex(next.X, next.Y);
                if (previous[nextIndex] >= 0 || !CanTraverse(map, next, allowRepairs, allowInteriorWallRepair))
                {
                    continue;
                }

                previous[nextIndex] = map.GetIndex(current.X, current.Y);
                queue.Enqueue(next);
            }
        }

        if (previous[targetIndex] < 0)
        {
            path = null;
            return false;
        }

        path = [];
        for (int index = targetIndex; ; index = previous[index])
        {
            path.Add(new LocalCoord(index % LocalMap.Width, index / LocalMap.Width));
            if (index == startIndex)
            {
                break;
            }
        }

        path.Reverse();
        return true;
    }

    private static bool CanTraverse(
        LocalMap map,
        LocalCoord coord,
        bool allowRepairs,
        bool allowInteriorWallRepair)
    {
        if (!map.BlocksMovement(coord))
        {
            return true;
        }

        if (!allowRepairs)
        {
            return false;
        }

        TerrainId terrain = map.Terrain[map.GetIndex(coord.X, coord.Y)];
        return terrain switch
        {
            TerrainId.Tree or TerrainId.Rock or TerrainId.DenseCanopy or TerrainId.Undergrowth => true,
            TerrainId.InteriorWall => allowInteriorWallRepair,
            _ => false
        };
    }

    private static float MeasureReachability(
        LocalMap map,
        LocalCoord entryPoint,
        out int reachable,
        out int walkable)
    {
        reachable = 0;
        walkable = 0;
        if (!map.Contains(entryPoint))
        {
            return 0f;
        }

        var visited = new bool[LocalMap.Width * LocalMap.Height];
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                if (!map.BlocksMovement(new LocalCoord(x, y)))
                {
                    walkable++;
                }
            }
        }

        if (walkable == 0)
        {
            return 0f;
        }

        if (map.BlocksMovement(entryPoint))
        {
            return 0f;
        }

        var queue = new Queue<LocalCoord>();
        queue.Enqueue(entryPoint);
        visited[map.GetIndex(entryPoint.X, entryPoint.Y)] = true;
        reachable = 1;

        while (queue.Count > 0)
        {
            LocalCoord current = queue.Dequeue();
            foreach ((int dx, int dy) in Cardinal)
            {
                int nx = current.X + dx;
                int ny = current.Y + dy;
                if (nx < 0 || ny < 0 || nx >= LocalMap.Width || ny >= LocalMap.Height)
                {
                    continue;
                }

                int index = map.GetIndex(nx, ny);
                if (visited[index] || map.BlocksMovement(new LocalCoord(nx, ny)))
                {
                    continue;
                }

                visited[index] = true;
                reachable++;
                queue.Enqueue(new LocalCoord(nx, ny));
            }
        }

        return reachable / (float)walkable;
    }

    private static void RepairEntryPoint(LocalMap map, LocalCoord entryPoint)
    {
        if (!map.Contains(entryPoint))
        {
            return;
        }

        map.SetTerrain(entryPoint.X, entryPoint.Y, TerrainId.Grass, TileFlags.None);
    }

    private static bool RepairBlockingTiles(LocalMap map, LocalCoord entryPoint, int ring)
    {
        bool repaired = false;
        int radius = 4 + ring * 3;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = entryPoint.X + dx;
                int y = entryPoint.Y + dy;
                if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
                {
                    continue;
                }

                var coord = new LocalCoord(x, y);
                if (!map.BlocksMovement(coord))
                {
                    continue;
                }

                int index = map.GetIndex(x, y);
                TerrainId terrain = map.Terrain[index];
                if (terrain is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Fence
                    or TerrainId.DeepWater or TerrainId.CavernWall)
                {
                    continue;
                }

                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
                repaired = true;
            }
        }

        return repaired;
    }

    private static readonly (int dx, int dy)[] Cardinal =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
    ];
}

public readonly record struct ValidationResult(
    float ReachabilityShare,
    int ReachableTiles,
    int WalkableTiles,
    int RepairAttempts);
