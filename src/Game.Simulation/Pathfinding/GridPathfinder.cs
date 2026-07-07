using Game.Simulation.LocalMaps;

namespace Game.Simulation.Pathfinding;

public static class GridPathfinder
{
    private static readonly (int dx, int dy)[] Directions =
    [
        (0, -1),
        (0, 1),
        (-1, 0),
        (1, 0),
    ];

    public static int GetTerrainMoveCost(TerrainId terrain)
    {
        return terrain switch
        {
            TerrainId.Road => 1,
            TerrainId.Grass => 2,
            TerrainId.Dirt => 3,
            TerrainId.Mud => 5,
            TerrainId.ShallowWater => 8,
            TerrainId.DeepWater => 10,
            TerrainId.Sand => 3,
            TerrainId.Rock => 6,
            TerrainId.Tree => 4,
            TerrainId.Concrete => 2,
            TerrainId.Floor => 2,
            TerrainId.Door => 2,
            TerrainId.TunnelFloor => 2,
            TerrainId.Dock => 2,
            TerrainId.Fence => 8,
            TerrainId.Wall => 10,
            TerrainId.CavernWall => 10,
            TerrainId.RuinStone => 4,
            TerrainId.InteriorWall => 10,
            TerrainId.Counter => 4,
            TerrainId.Window => 2,
            TerrainId.Rubble => 4,
            TerrainId.Machinery => 6,
            TerrainId.StairsUp => 2,
            TerrainId.StairsDown => 2,
            TerrainId.StructureExit => 2,
            _ => 3
        };
    }

    public static List<(int X, int Y)> FindPath(
        int startX,
        int startY,
        int targetX,
        int targetY,
        int width,
        int height,
        Func<int, int, bool> isBlocked,
        Func<int, int, int>? moveCost = null,
        Func<int, int, int, int, int>? stepCost = null)
    {
        moveCost ??= static (_, _) => 1;

        var result = new List<(int X, int Y)>();

        if (startX == targetX && startY == targetY)
        {
            return result;
        }

        if (!InBounds(targetX, targetY, width, height) || isBlocked(targetX, targetY))
        {
            return result;
        }

        int cellCount = width * height;
        var gScore = new int[cellCount];
        Array.Fill(gScore, int.MaxValue);
        var cameFrom = new int[cellCount];
        Array.Fill(cameFrom, -1);

        int startIndex = startY * width + startX;
        int targetIndex = targetY * width + targetX;
        gScore[startIndex] = 0;

        var open = new PriorityQueue<int, int>();
        open.Enqueue(startIndex, Manhattan(targetX, targetY, startX, startY));

        while (open.Count > 0)
        {
            int current = open.Dequeue();
            if (current == targetIndex)
            {
                ReconstructPath(cameFrom, current, width, result);
                return result;
            }

            int cx = current % width;
            int cy = current / width;

            foreach ((int dx, int dy) in Directions)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (!InBounds(nx, ny, width, height) || isBlocked(nx, ny))
                {
                    continue;
                }

                int neighbor = ny * width + nx;
                int edgeCost = stepCost is not null
                    ? stepCost(cx, cy, nx, ny)
                    : moveCost(nx, ny);
                int tentative = gScore[current] + edgeCost;
                if (tentative >= gScore[neighbor])
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentative;
                open.Enqueue(neighbor, tentative + Manhattan(targetX, targetY, nx, ny));
            }
        }

        return result;
    }

    private static void ReconstructPath(int[] cameFrom, int current, int width, List<(int X, int Y)> result)
    {
        var reversed = new Stack<(int X, int Y)>();
        while (cameFrom[current] != -1)
        {
            int x = current % width;
            int y = current / width;
            reversed.Push((x, y));
            current = cameFrom[current];
        }

        while (reversed.Count > 0)
        {
            result.Add(reversed.Pop());
        }
    }

    private static int Manhattan(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }

    private static bool InBounds(int x, int y, int width, int height)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }
}
