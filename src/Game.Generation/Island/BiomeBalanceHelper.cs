using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class BiomeBalanceHelper
{
    public static void EnsureBiomeFloor(IslandPlan plan, ulong stageSeed, float minShare = 0.025f)
    {
        var landCells = CollectLandCells(plan);
        if (landCells.Count == 0)
        {
            return;
        }

        BiomeId[] required =
        [
            BiomeId.Plains,
            BiomeId.Forest,
            BiomeId.Jungle,
            BiomeId.Hills,
            BiomeId.Swamp,
            BiomeId.Mountains,
            BiomeId.Volcanic
        ];

        int minCells = Math.Max(4, (int)(landCells.Count * minShare));
        var random = new DeterministicRandom(SeedUtility.DeriveStage(stageSeed, 44));

        foreach (BiomeId biome in required)
        {
            int current = landCells.Count(pos => plan.GetCell(pos.X, pos.Y).Biome == biome);
            int needed = minCells - current;
            for (int i = 0; i < needed; i++)
            {
                (int X, int Y) pick = landCells[random.NextInt(landCells.Count)];
                ref IslandCellData cell = ref plan.GetCell(pick.X, pick.Y);
                if (cell.IsCoast || IsProtectedLandmarkCell(plan, pick.X, pick.Y))
                {
                    continue;
                }

                cell.Biome = biome;
            }
        }
    }

    public static bool ReduceDominantBiome(
        IslandPlan plan,
        DeterministicRandom random,
        BiomeId[] alternatives,
        float maxShare)
    {
        var landCells = CollectLandCellsWithBiome(plan);
        if (landCells.Count == 0)
        {
            return false;
        }

        int maxCells = Math.Max(8, (int)(landCells.Count * maxShare));
        IGrouping<BiomeId, (int X, int Y, BiomeId Biome)>? dominant = landCells
            .GroupBy(cell => cell.Biome)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault(group => group.Count() > maxCells);

        if (dominant is null)
        {
            return false;
        }

        int excess = dominant.Count() - maxCells;
        foreach ((int x, int y, BiomeId biome) in dominant.OrderBy(_ => random.NextFloat()).Take(excess))
        {
            if (IsProtectedLandmarkCell(plan, x, y))
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(x, y);
            if (cell.VolcanicActivity > 0.2f && cell.Biome == BiomeId.Volcanic)
            {
                continue;
            }

            BiomeId replacement = alternatives[random.NextInt(alternatives.Length)];
            if (replacement == biome)
            {
                replacement = BiomeId.Plains;
            }

            cell.Biome = replacement;
        }

        return true;
    }

    public static void StampVisitorCenterPlains(IslandPlan plan, int radius = 6)
    {
        if (plan.VisitorCenterCell.X < 0)
        {
            return;
        }

        ref IslandCellData visitorCell = ref plan.GetCell(plan.VisitorCenterCell);
        visitorCell.Biome = BiomeId.Plains;
        visitorCell.IsCoast = false;
        StampRadius(plan, plan.VisitorCenterCell, radius, BiomeId.Plains);
    }

    public static bool IsProtectedLandmarkCell(IslandPlan plan, int x, int y)
    {
        if (plan.VisitorCenterCell.X < 0)
        {
            return false;
        }

        int dx = x - plan.VisitorCenterCell.X;
        int dy = y - plan.VisitorCenterCell.Y;
        return dx * dx + dy * dy <= 9;
    }

    public static float MeasureWetBiomeShare(IslandPlan plan)
    {
        List<(int X, int Y, BiomeId Biome)> landCells = CollectLandCellsWithBiome(plan);
        if (landCells.Count == 0)
        {
            return 0f;
        }

        int wetCount = landCells.Count(cell => cell.Biome is BiomeId.Swamp or BiomeId.Jungle);
        return wetCount / (float)landCells.Count;
    }

    public static float MeasureLandElevationStdDev(IslandPlan plan)
    {
        var elevations = new List<float>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsLand)
                {
                    elevations.Add(cell.Elevation);
                }
            }
        }

        if (elevations.Count < 2)
        {
            return 0f;
        }

        float mean = elevations.Average();
        float variance = elevations.Average(elevation => (elevation - mean) * (elevation - mean));
        return MathF.Sqrt(variance);
    }

    public static void CorrectExcessWetness(IslandPlan plan, float maxShare, DeterministicRandom random)
    {
        List<(int X, int Y, BiomeId Biome)> landCells = CollectLandCellsWithBiome(plan);
        if (landCells.Count == 0)
        {
            return;
        }

        int wetCount = landCells.Count(cell => cell.Biome is BiomeId.Swamp or BiomeId.Jungle);
        int maxWetCells = Math.Max(4, (int)(landCells.Count * maxShare));
        int toConvert = wetCount - maxWetCells;
        if (toConvert <= 0)
        {
            return;
        }

        var wetCells = landCells
            .Where(cell => cell.Biome is BiomeId.Swamp or BiomeId.Jungle)
            .Select(cell => (cell.X, cell.Y, plan.GetCell(cell.X, cell.Y).Moisture))
            .OrderBy(cell => cell.Moisture)
            .ThenBy(_ => random.NextFloat())
            .Take(toConvert);

        foreach ((int x, int y, _) in wetCells)
        {
            if (IsProtectedLandmarkCell(plan, x, y))
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(x, y);
            cell.Biome = cell.Elevation > 0.62f ? BiomeId.Forest : BiomeId.Plains;
        }
    }

    public static void InjectRelief(IslandPlan plan, float minStdDev, DeterministicRandom random)
    {
        if (MeasureLandElevationStdDev(plan) >= minStdDev)
        {
            return;
        }

        List<(int X, int Y)> landCells = CollectLandCells(plan)
            .Where(pos => !IsProtectedLandmarkCell(plan, pos.X, pos.Y))
            .Where(pos => !plan.GetCell(pos.X, pos.Y).IsCoast)
            .ToList();

        if (landCells.Count == 0)
        {
            return;
        }

        int injections = Math.Clamp(landCells.Count / 180, 12, 96);
        for (int i = 0; i < injections; i++)
        {
            (int x, int y) = landCells[random.NextInt(landCells.Count)];
            ref IslandCellData cell = ref plan.GetCell(x, y);
            cell.Elevation = Math.Min(1f, cell.Elevation + 0.10f + random.NextFloat() * 0.10f);
            cell.TectonicUplift += 0.05f;

            if (cell.Elevation >= 0.82f)
            {
                cell.Biome = BiomeId.Mountains;
            }
            else if (cell.Elevation >= 0.68f)
            {
                cell.Biome = BiomeId.Hills;
            }
        }
    }

    private static List<(int X, int Y)> CollectLandCells(IslandPlan plan)
    {
        var landCells = new List<(int X, int Y)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsLand && !cell.IsCoast)
                {
                    landCells.Add((x, y));
                }
            }
        }

        return landCells;
    }

    private static List<(int X, int Y, BiomeId Biome)> CollectLandCellsWithBiome(IslandPlan plan)
    {
        var landCells = new List<(int X, int Y, BiomeId Biome)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsLand && !cell.IsCoast)
                {
                    landCells.Add((x, y, cell.Biome));
                }
            }
        }

        return landCells;
    }

    private static void StampRadius(IslandPlan plan, WorldCoord center, int radius, BiomeId biome)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                int x = center.X + dx;
                int y = center.Y + dy;
                if (!plan.Contains(x, y) || !plan.IsLand(x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsCoast)
                {
                    continue;
                }

                cell.Biome = biome;
            }
        }
    }
}
