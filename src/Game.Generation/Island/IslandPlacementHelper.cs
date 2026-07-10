using Game.Generation.Island.Stages;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class IslandPlacementHelper
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

    public static bool CanPlaceFootprint(IslandPlan plan, int globalOriginX, int globalOriginY, int width, int height)
    {
        if (globalOriginX < 0 || globalOriginY < 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        int globalLimitX = plan.Width * Game.Simulation.LocalMaps.LocalMap.Width;
        int globalLimitY = plan.Height * Game.Simulation.LocalMaps.LocalMap.Height;
        if (globalOriginX > globalLimitX - width || globalOriginY > globalLimitY - height)
        {
            return false;
        }

        int minCellX = globalOriginX / Game.Simulation.LocalMaps.LocalMap.Width;
        int minCellY = globalOriginY / Game.Simulation.LocalMaps.LocalMap.Height;
        int maxCellX = (globalOriginX + width - 1) / Game.Simulation.LocalMaps.LocalMap.Width;
        int maxCellY = (globalOriginY + height - 1) / Game.Simulation.LocalMaps.LocalMap.Height;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                if (!plan.Contains(cellX, cellY) || !plan.IsLand(cellX, cellY))
                {
                    return false;
                }

                ref IslandCellData cell = ref plan.GetCell(cellX, cellY);
                if (cell.IsCoast
                    || plan.VolcanoExclusion.IsProtected(cellX, cellY)
                    || plan.LavaFlowGraph.PathCells.Contains((cellX, cellY)))
                {
                    return false;
                }

                IslandCellRole blockingRole = cell.Role & ~IslandCellRole.Road;
                if (BiomeBalanceHelper.HasEnterableRole(blockingRole))
                {
                    return false;
                }
            }
        }

        foreach (StructurePlacement existing in plan.Structures)
        {
            if (FootprintsOverlap(
                    globalOriginX,
                    globalOriginY,
                    width,
                    height,
                    existing.GlobalOriginX,
                    existing.GlobalOriginY,
                    existing.Width,
                    existing.Height))
            {
                return false;
            }
        }

        return true;
    }

    public static void MarkFootprintRoles(IslandPlan plan, int globalOriginX, int globalOriginY, int width, int height, IslandCellRole role)
    {
        int minCellX = globalOriginX / Game.Simulation.LocalMaps.LocalMap.Width;
        int minCellY = globalOriginY / Game.Simulation.LocalMaps.LocalMap.Height;
        int maxCellX = (globalOriginX + width - 1) / Game.Simulation.LocalMaps.LocalMap.Width;
        int maxCellY = (globalOriginY + height - 1) / Game.Simulation.LocalMaps.LocalMap.Height;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                MarkRole(plan, new WorldCoord(cellX, cellY), role);
            }
        }
    }

    private static bool FootprintsOverlap(
        int ax,
        int ay,
        int aw,
        int ah,
        int bx,
        int by,
        int bw,
        int bh)
    {
        return ax < bx + bw &&
               ax + aw > bx &&
               ay < by + bh &&
               ay + ah > by;
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
