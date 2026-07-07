using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Scenarios;

public static class ScenarioCellPicker
{
    public static WorldCoord? FindCellWithRole(IslandPlan plan, IslandCellRole role, ulong seed, int ordinal)
    {
        var matches = new List<WorldCoord>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                if (plan.GetCell(x, y).Role.HasFlag(role))
                {
                    matches.Add(new WorldCoord(x, y));
                }
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        matches.Sort(static (a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        ulong pick = SeedUtility.DeriveStage(seed, (uint)((int)role + ordinal * 17));
        int index = (int)(pick % (ulong)matches.Count);
        return matches[index];
    }

    public static WorldCoord? FindCellWithBiome(IslandPlan plan, BiomeId biome, ulong seed, int ordinal)
    {
        var matches = new List<WorldCoord>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                if (plan.GetCell(x, y).Biome == biome)
                {
                    matches.Add(new WorldCoord(x, y));
                }
            }
        }

        if (matches.Count == 0)
        {
            return null;
        }

        matches.Sort(static (a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        ulong pick = SeedUtility.DeriveStage(seed, (uint)((int)biome + ordinal * 23));
        int index = (int)(pick % (ulong)matches.Count);
        return matches[index];
    }

    public static WorldCoord? PickDistinctCell(
        IslandPlan plan,
        ulong seed,
        int ordinal,
        params WorldCoord?[] reserved)
    {
        var reservedSet = new HashSet<WorldCoord>();
        foreach (WorldCoord? coord in reserved)
        {
            if (coord is WorldCoord value)
            {
                reservedSet.Add(value);
            }
        }

        var candidates = new List<WorldCoord>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                var coord = new WorldCoord(x, y);
                if (reservedSet.Contains(coord))
                {
                    continue;
                }

                if (BiomeTraversal.IsPassable(plan.GetCell(x, y).Biome))
                {
                    candidates.Add(coord);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort(static (a, b) =>
        {
            int cmp = a.Y.CompareTo(b.Y);
            return cmp != 0 ? cmp : a.X.CompareTo(b.X);
        });

        ulong pick = SeedUtility.DeriveStage(seed, (uint)(ordinal * 31 + 7));
        int index = (int)(pick % (ulong)candidates.Count);
        return candidates[index];
    }
}
