using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Regional;

public static class GlobalTilePathUtility
{
    public static void AddPathWithBorderRuns(
        HashSet<(int GlobalX, int GlobalY)> tiles,
        IReadOnlyList<WorldCoord> path,
        int width)
    {
        if (path.Count == 0)
        {
            return;
        }

        AddTileWithWidth(tiles, path[0], width);

        for (int i = 1; i < path.Count; i++)
        {
            AddSegmentTiles(tiles, path[i - 1], path[i], width);
        }
    }

    public static void AddSegmentTiles(
        HashSet<(int GlobalX, int GlobalY)> tiles,
        WorldCoord from,
        WorldCoord to,
        int width)
    {
        (int fromGx, int fromGy) = FacilityRoadGraph.CellCenterGlobal(from);
        (int toGx, int toGy) = FacilityRoadGraph.CellCenterGlobal(to);

        AddGlobalSegment(tiles, fromGx, fromGy, toGx, toGy, width);
    }

    public static void AddGlobalSegment(
        HashSet<(int GlobalX, int GlobalY)> tiles,
        int fromGlobalX,
        int fromGlobalY,
        int toGlobalX,
        int toGlobalY,
        int width)
    {
        int x = fromGlobalX;
        int y = fromGlobalY;

        while (x != toGlobalX)
        {
            AddGlobalTileWithWidth(tiles, x, y, width, horizontal: true);
            x += Math.Sign(toGlobalX - x);
        }

        while (y != toGlobalY)
        {
            AddGlobalTileWithWidth(tiles, x, y, width, horizontal: false);
            y += Math.Sign(toGlobalY - y);
        }

        bool finalHorizontal = fromGlobalY == toGlobalY;
        AddGlobalTileWithWidth(tiles, toGlobalX, toGlobalY, width, finalHorizontal);
    }

    public static void AddGlobalTileWithWidth(
        HashSet<(int GlobalX, int GlobalY)> tiles,
        int globalX,
        int globalY,
        int width,
        bool horizontal)
    {
        int clampedWidth = Math.Max(1, width);
        int firstOffset = -(clampedWidth - 1) / 2;
        for (int offset = firstOffset; offset < firstOffset + clampedWidth; offset++)
        {
            tiles.Add(horizontal
                ? (globalX, globalY + offset)
                : (globalX + offset, globalY));
        }
    }

    private static void AddTileWithWidth(
        HashSet<(int GlobalX, int GlobalY)> tiles,
        WorldCoord cell,
        int width)
    {
        (int centerGx, int centerGy) = FacilityRoadGraph.CellCenterGlobal(cell);
        AddGlobalTileWithWidth(tiles, centerGx, centerGy, width, horizontal: true);
    }

    public static bool TryComputeEdgeLocalOffset(
        HashSet<(int GlobalX, int GlobalY)> globalTiles,
        WorldCoord fromCell,
        WorldCoord toCell,
        Direction edge,
        int width,
        out int localOffset)
    {
        int cellMinX = fromCell.X * LocalMap.Width;
        int cellMinY = fromCell.Y * LocalMap.Height;
        var crossingCoords = new List<int>();

        foreach ((int globalX, int globalY) in globalTiles)
        {
            if (!IsOnSharedEdge(fromCell, toCell, edge, globalX, globalY))
            {
                continue;
            }

            int localCoord = edge is Direction.East or Direction.West
                ? globalY - cellMinY
                : globalX - cellMinX;
            crossingCoords.Add(localCoord);
        }

        if (crossingCoords.Count == 0)
        {
            localOffset = 0;
            return false;
        }

        crossingCoords.Sort();
        int median = crossingCoords[crossingCoords.Count / 2];
        int maxOffset = edge is Direction.East or Direction.West
            ? LocalMap.Height - width
            : LocalMap.Width - width;
        localOffset = Math.Clamp(median - width / 2, 0, maxOffset);
        return true;
    }

    private static bool IsOnSharedEdge(
        WorldCoord fromCell,
        WorldCoord toCell,
        Direction edge,
        int globalX,
        int globalY)
    {
        int cellMinX = fromCell.X * LocalMap.Width;
        int cellMinY = fromCell.Y * LocalMap.Height;
        int cellMaxX = cellMinX + LocalMap.Width - 1;
        int cellMaxY = cellMinY + LocalMap.Height - 1;

        return edge switch
        {
            Direction.East when toCell.X == fromCell.X + 1 =>
                globalX >= cellMaxX - 1 && globalX <= cellMaxX + 1 &&
                globalY >= cellMinY && globalY <= cellMaxY,
            Direction.West when toCell.X == fromCell.X - 1 =>
                globalX >= cellMinX - 1 && globalX <= cellMinX + 1 &&
                globalY >= cellMinY && globalY <= cellMaxY,
            Direction.South when toCell.Y == fromCell.Y + 1 =>
                globalY >= cellMaxY - 1 && globalY <= cellMaxY + 1 &&
                globalX >= cellMinX && globalX <= cellMaxX,
            Direction.North when toCell.Y == fromCell.Y - 1 =>
                globalY >= cellMinY - 1 && globalY <= cellMinY + 1 &&
                globalX >= cellMinX && globalX <= cellMaxX,
            _ => false
        };
    }
}
