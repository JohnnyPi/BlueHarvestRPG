using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

internal static class IslandPlacementHelper
{
    public static (int GlobalX, int GlobalY) CellCenterGlobal(WorldCoord cell)
    {
        return (
            cell.X * Game.Simulation.LocalMaps.LocalMap.Width + Game.Simulation.LocalMaps.LocalMap.Width / 2,
            cell.Y * Game.Simulation.LocalMaps.LocalMap.Height + Game.Simulation.LocalMaps.LocalMap.Height / 2);
    }

    public static (int GlobalX, int GlobalY) CenteredOrigin(WorldCoord cell, int width, int height)
    {
        return (
            cell.X * Game.Simulation.LocalMaps.LocalMap.Width + (Game.Simulation.LocalMaps.LocalMap.Width - width) / 2,
            cell.Y * Game.Simulation.LocalMaps.LocalMap.Height + (Game.Simulation.LocalMaps.LocalMap.Height - height) / 2);
    }

    public static List<WorldCoord> FindCoastalCells(IslandPlan plan)
    {
        var results = new List<WorldCoord>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsCoast)
                {
                    results.Add(new WorldCoord(x, y));
                }
            }
        }

        return results;
    }

    public static List<WorldCoord> FindLandCellsInRegion(IslandPlan plan, int regionId)
    {
        var results = new List<WorldCoord>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (plan.GetRegionId(x, y) == regionId && plan.IsLand(x, y) && !plan.GetCell(x, y).IsCoast)
                {
                    results.Add(new WorldCoord(x, y));
                }
            }
        }

        return results;
    }

    public static WorldCoord? FindCentralMainIslandCell(IslandPlan plan)
    {
        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;

        WorldCoord? best = null;
        float bestScore = float.MaxValue;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast)
                {
                    continue;
                }

                int regionId = plan.GetRegionId(x, y);
                IslandRegion? region = plan.Regions.FirstOrDefault(r => r.Id == regionId);
                if (region is null || !region.IsMainIsland)
                {
                    continue;
                }

                float dx = x - centerX;
                float dy = y - centerY;
                float score = dx * dx + dy * dy;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new WorldCoord(x, y);
                }
            }
        }

        return best;
    }

    public static List<WorldCoord> PickSpreadCells(List<WorldCoord> candidates, int count, ulong seed)
    {
        if (candidates.Count == 0 || count <= 0)
        {
            return [];
        }

        var random = new DeterministicRandom(seed);
        var pool = candidates.ToList();

        for (int i = 0; i < pool.Count; i++)
        {
            int swapIndex = i + random.NextInt(pool.Count - i);
            (pool[i], pool[swapIndex]) = (pool[swapIndex], pool[i]);
        }

        var picked = new List<WorldCoord>();

        foreach (WorldCoord candidate in pool)
        {
            bool tooClose = picked.Any(existing =>
            {
                int dx = existing.X - candidate.X;
                int dy = existing.Y - candidate.Y;
                return dx * dx + dy * dy < 100;
            });

            if (!tooClose)
            {
                picked.Add(candidate);
            }

            if (picked.Count >= count)
            {
                break;
            }
        }

        if (picked.Count < count)
        {
            foreach (WorldCoord candidate in pool)
            {
                if (picked.Contains(candidate))
                {
                    continue;
                }

                picked.Add(candidate);
                if (picked.Count >= count)
                {
                    break;
                }
            }
        }

        return picked;
    }

    public static List<WorldCoord> SampleLandCells(
        IslandPlan plan,
        Func<IslandCellData, bool> predicate,
        int count,
        ulong seed)
    {
        var random = new DeterministicRandom(seed);
        var picked = new List<WorldCoord>();
        int seen = 0;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || !predicate(cell))
                {
                    continue;
                }

                seen++;
                if (picked.Count < count)
                {
                    picked.Add(new WorldCoord(x, y));
                    continue;
                }

                int replaceIndex = random.NextInt(seen);
                if (replaceIndex < count)
                {
                    picked[replaceIndex] = new WorldCoord(x, y);
                }
            }
        }

        return picked;
    }

    public static void MarkRole(IslandPlan plan, WorldCoord cell, IslandCellRole role)
    {
        ref IslandCellData data = ref plan.GetCell(cell);
        data.Role |= role;
    }
}
