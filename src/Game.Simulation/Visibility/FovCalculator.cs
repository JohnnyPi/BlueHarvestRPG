using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.Visibility;

public static class FovCalculator
{
    public const int DefaultPlayerRadius = 12;
    public const int DefaultCreatureRadius = 10;

    public static void Compute(LocalMap map, LocalCoord origin, bool[] visible)
    {
        ComputeVisible(map, origin, DefaultPlayerRadius, visible);

        if (!map.Contains(origin))
        {
            return;
        }

        for (int i = 0; i < visible.Length; i++)
        {
            if (visible[i])
            {
                map.Explored[i] = true;
            }
        }
    }

    public static void ComputeVisible(LocalMap map, LocalCoord origin, int radius, bool[] visible)
    {
        Array.Clear(visible, 0, visible.Length);

        if (!map.Contains(origin))
        {
            return;
        }

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var target = new LocalCoord(origin.X + dx, origin.Y + dy);
                if (!map.Contains(target))
                {
                    continue;
                }

                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                if (HasLineOfSight(map, origin, target))
                {
                    visible[map.GetIndex(target.X, target.Y)] = true;
                }
            }
        }

        int originIndex = map.GetIndex(origin.X, origin.Y);
        visible[originIndex] = true;
    }

    public static bool HasLineOfSight(LocalMap map, LocalCoord from, LocalCoord to)
    {
        int x0 = from.X;
        int y0 = from.Y;
        int x1 = to.X;
        int y1 = to.Y;

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            var coord = new LocalCoord(x0, y0);
            if (coord != from && coord != to && map.BlocksVision(coord))
            {
                return false;
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return true;
    }

    public static bool CanSee(LocalMap map, LocalCoord from, LocalCoord to, int radius)
    {
        if (!map.Contains(from) || !map.Contains(to))
        {
            return false;
        }

        int dx = to.X - from.X;
        int dy = to.Y - from.Y;
        if (dx * dx + dy * dy > radius * radius)
        {
            return false;
        }

        return HasLineOfSight(map, from, to);
    }
}
