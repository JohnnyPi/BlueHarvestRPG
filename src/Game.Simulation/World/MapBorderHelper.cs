using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.World;

public static class MapBorderHelper
{
    public static bool IsBorderTile(int localX, int localY)
    {
        return localX == 0 ||
               localY == 0 ||
               localX == LocalMap.Width - 1 ||
               localY == LocalMap.Height - 1;
    }

    public static bool IsOnEdge(Direction edge, int localX, int localY)
    {
        return edge switch
        {
            Direction.West => localX == 0,
            Direction.East => localX == LocalMap.Width - 1,
            Direction.North => localY == 0,
            Direction.South => localY == LocalMap.Height - 1,
            _ => false
        };
    }

    public static bool TryResolveBorderTransition(
        Overworld overworld,
        WorldCoord currentWorld,
        Direction edge,
        int borderLocalX,
        int borderLocalY,
        out MapTransition transition)
    {
        transition = default;

        if (!IsOnEdge(edge, borderLocalX, borderLocalY))
        {
            return false;
        }

        int deltaX = edge switch
        {
            Direction.West => -1,
            Direction.East => 1,
            _ => 0
        };

        int deltaY = edge switch
        {
            Direction.North => -1,
            Direction.South => 1,
            _ => 0
        };

        return MapTransitionResolver.TryResolve(
            overworld,
            currentWorld,
            new LocalCoord(borderLocalX, borderLocalY),
            deltaX,
            deltaY,
            out transition);
    }

    public static IEnumerable<Direction> GetTransitionEdges(int localX, int localY)
    {
        if (localX == 0)
        {
            yield return Direction.West;
        }

        if (localX == LocalMap.Width - 1)
        {
            yield return Direction.East;
        }

        if (localY == 0)
        {
            yield return Direction.North;
        }

        if (localY == LocalMap.Height - 1)
        {
            yield return Direction.South;
        }
    }

    public static string FormatTransitionLabel(Direction edge, bool includeDirection)
    {
        if (!includeDirection)
        {
            return "Go to Next Map";
        }

        return edge switch
        {
            Direction.North => "Go to Next Map (North)",
            Direction.East => "Go to Next Map (East)",
            Direction.South => "Go to Next Map (South)",
            Direction.West => "Go to Next Map (West)",
            _ => "Go to Next Map"
        };
    }
}
