using Game.Content.Definitions;
using Game.Generation.Island.Fields;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class BiomeBalanceHelper
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public const IslandCellRole EnterableRoles =
        IslandCellRole.VisitorCenter
        | IslandCellRole.Dock
        | IslandCellRole.Helipad
        | IslandCellRole.Hotel
        | IslandCellRole.Restaurant
        | IslandCellRole.Attraction
        | IslandCellRole.Maintenance
        | IslandCellRole.Paddock
        | IslandCellRole.Tunnel
        | IslandCellRole.Cavern
        | IslandCellRole.Ruin
        | IslandCellRole.Fortification
        | IslandCellRole.Road;

    public static bool HasEnterableRole(IslandCellRole role) => (role & EnterableRoles) != 0;

    public static void StampFacilityBiomes(IslandPlan plan)
    {
        StampVisitorCenterPlains(plan);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if ((cell.Role & IslandCellRole.Road) != 0)
                {
                    if (!cell.IsCoast)
                    {
                        cell.Biome = BiomeId.Plains;
                    }

                    continue;
                }

                if (!HasEnterableRole(cell.Role))
                {
                    continue;
                }

                if ((cell.Role & IslandCellRole.Dock) != 0 && cell.IsCoast)
                {
                    cell.Biome = BiomeId.Beach;
                }
                else if (!cell.IsCoast)
                {
                    cell.Biome = BiomeId.Plains;
                }
            }
        }
    }

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
            BiomeId.Mountains
        ];

        int minCells = Math.Max(4, (int)(landCells.Count * minShare));
        var random = new DeterministicRandom(SeedUtility.DeriveStage(stageSeed, 44));

        foreach (BiomeId biome in required)
        {
            int current = landCells.Count(pos => plan.GetCell(pos.X, pos.Y).Biome == biome);
            int needed = minCells - current;
            if (needed <= 0)
            {
                continue;
            }

            GrowBiomePatch(plan, biome, needed, random);
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
        BiomeId dominantBiome = dominant.Key;
        var components = IslandQualityMetrics.FindBiomeComponents(plan, dominantBiome)
            .OrderBy(component => component.Count)
            .ToList();

        int converted = 0;
        foreach (List<int> component in components)
        {
            if (converted >= excess)
            {
                break;
            }

            var borderCells = component
                .SelectMany(index => GetBorderNeighbors(plan, index, component))
                .Distinct()
                .OrderBy(index => ScoreShrinkCandidate(plan, index, dominantBiome, random))
                .ToList();

            foreach (int index in borderCells)
            {
                if (converted >= excess)
                {
                    break;
                }

                int x = index % plan.Width;
                int y = index / plan.Width;
                if (IsProtectedLandmarkCell(plan, x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.Cells[index];
                if (cell.VolcanicActivity > 0.2f && cell.Biome == BiomeId.Volcanic)
                {
                    continue;
                }

                BiomeId replacement = PickAlternative(alternatives, dominantBiome, cell, random);
                cell.Biome = replacement;
                converted++;
            }
        }

        return converted > 0;
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
        if (!plan.Contains(x, y) || !plan.IsLand(x, y))
        {
            return false;
        }

        ref IslandCellData cell = ref plan.GetCell(x, y);
        if (HasEnterableRole(cell.Role))
        {
            return true;
        }

        if (plan.VisitorCenterCell.X < 0)
        {
            return false;
        }

        int dx = x - plan.VisitorCenterCell.X;
        int dy = y - plan.VisitorCenterCell.Y;
        return dx * dx + dy * dy <= 36;
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

        var wetComponents = new List<List<int>>();
        foreach (BiomeId wetBiome in new[] { BiomeId.Swamp, BiomeId.Jungle })
        {
            wetComponents.AddRange(IslandQualityMetrics.FindBiomeComponents(plan, wetBiome));
        }

        wetComponents = wetComponents
            .OrderBy(component => component.Count)
            .ThenBy(_ => random.NextFloat())
            .ToList();

        int converted = 0;
        foreach (List<int> component in wetComponents)
        {
            if (converted >= toConvert)
            {
                break;
            }

            if (component.Any(index => IsProtectedLandmarkCell(plan, index % plan.Width, index / plan.Width)))
            {
                continue;
            }

            ref IslandCellData sample = ref plan.Cells[component[0]];
            BiomeId replacement = sample.Elevation > 0.62f ? BiomeId.Forest : BiomeId.Plains;
            foreach (int index in component)
            {
                if (converted >= toConvert)
                {
                    break;
                }

                plan.Cells[index].Biome = replacement;
                converted++;
            }
        }
    }

    public static void InjectRelief(IslandPlan plan, float minStdDev, DeterministicRandom random, IReadOnlyList<IslandRidgeDefinition> ridges)
    {
        if (MeasureLandElevationStdDev(plan) >= minStdDev)
        {
            return;
        }

        List<(int X, int Y)> candidates = CollectLandCells(plan)
            .Where(pos => !IsProtectedLandmarkCell(plan, pos.X, pos.Y))
            .Where(pos => !plan.GetCell(pos.X, pos.Y).IsCoast)
            .OrderByDescending(pos => RidgeSplineField.SampleAtCell(pos.X, pos.Y, plan.Width, plan.Height, ridges))
            .ThenBy(_ => random.NextFloat())
            .Take(96)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        int injections = Math.Clamp(candidates.Count / 12, 8, 48);
        for (int i = 0; i < injections; i++)
        {
            (int x, int y) = candidates[i % candidates.Count];
            GrowReliefPatch(plan, x, y, random);
        }
    }

    private static void GrowBiomePatch(IslandPlan plan, BiomeId biome, int needed, DeterministicRandom random)
    {
        var frontier = new PriorityQueue<int, float>();
        var visited = new HashSet<int>();
        var existing = IslandQualityMetrics.FindBiomeComponents(plan, biome)
            .OrderByDescending(component => component.Count)
            .FirstOrDefault();

        if (existing is { Count: > 0 })
        {
            foreach (int index in existing)
            {
                EnqueueNeighbors(plan, index, biome, frontier, visited);
            }
        }
        else
        {
            (int X, int Y)? anchor = CollectLandCells(plan)
                .OrderByDescending(pos => BiomeSuitabilityHelper.ScoreBiomeForCell(biome, plan.GetCell(pos.X, pos.Y)))
                .ThenBy(_ => random.NextFloat())
                .FirstOrDefault();

            if (anchor is null)
            {
                return;
            }

            int index = anchor.Value.Y * plan.Width + anchor.Value.X;
            plan.Cells[index].Biome = biome;
            needed--;
            EnqueueNeighbors(plan, index, biome, frontier, visited);
        }

        while (needed > 0 && frontier.Count > 0)
        {
            int index = frontier.Dequeue();
            if (visited.Contains(index))
            {
                continue;
            }

            visited.Add(index);
            int x = index % plan.Width;
            int y = index / plan.Width;
            if (!plan.IsLand(x, y) || plan.GetCell(x, y).IsCoast || IsProtectedLandmarkCell(plan, x, y))
            {
                continue;
            }

            plan.Cells[index].Biome = biome;
            needed--;
            EnqueueNeighbors(plan, index, biome, frontier, visited);
        }
    }

    private static void EnqueueNeighbors(
        IslandPlan plan,
        int index,
        BiomeId biome,
        PriorityQueue<int, float> frontier,
        HashSet<int> visited)
    {
        int x = index % plan.Width;
        int y = index / plan.Width;
        foreach ((int dx, int dy) in Neighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!plan.Contains(nx, ny))
            {
                continue;
            }

            int neighborIndex = ny * plan.Width + nx;
            if (visited.Contains(neighborIndex))
            {
                continue;
            }

            float score = BiomeSuitabilityHelper.ScoreBiomeForCell(biome, plan.GetCell(nx, ny));
            frontier.Enqueue(neighborIndex, -score);
        }
    }

    private static void GrowReliefPatch(IslandPlan plan, int startX, int startY, DeterministicRandom random)
    {
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        var visited = new HashSet<(int X, int Y)> { (startX, startY) };
        int cells = 0;

        while (queue.Count > 0 && cells < 6)
        {
            (int x, int y) = queue.Dequeue();
            ref IslandCellData cell = ref plan.GetCell(x, y);
            if (!cell.IsLand || cell.IsCoast || IsProtectedLandmarkCell(plan, x, y))
            {
                continue;
            }

            cell.Elevation = Math.Min(1f, cell.Elevation + 0.08f + random.NextFloat() * 0.08f);
            cell.TectonicUplift += 0.04f;
            cell.Biome = cell.Elevation >= 0.82f ? BiomeId.Mountains : BiomeId.Hills;
            cells++;

            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.Contains(nx, ny) || !visited.Add((nx, ny)))
                {
                    continue;
                }

                queue.Enqueue((nx, ny));
            }
        }
    }

    private static IEnumerable<int> GetBorderNeighbors(IslandPlan plan, int index, List<int> component)
    {
        var componentSet = new HashSet<int>(component);
        int x = index % plan.Width;
        int y = index / plan.Width;

        foreach ((int dx, int dy) in Neighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!plan.Contains(nx, ny))
            {
                continue;
            }

            int neighborIndex = ny * plan.Width + nx;
            if (componentSet.Contains(neighborIndex))
            {
                yield return neighborIndex;
            }
        }
    }

    private static float ScoreShrinkCandidate(
        IslandPlan plan,
        int index,
        BiomeId dominantBiome,
        DeterministicRandom random)
    {
        ref IslandCellData cell = ref plan.Cells[index];
        return BiomeSuitabilityHelper.ScoreBiomeForCell(dominantBiome, cell) + random.NextFloat() * 0.01f;
    }

    private static BiomeId PickAlternative(
        BiomeId[] alternatives,
        BiomeId dominantBiome,
        IslandCellData cell,
        DeterministicRandom random)
    {
        BiomeId best = BiomeId.Plains;
        float bestScore = float.MinValue;
        foreach (BiomeId candidate in alternatives)
        {
            if (candidate == dominantBiome)
            {
                continue;
            }

            float score = BiomeSuitabilityHelper.ScoreBiomeForCell(candidate, cell) + random.NextFloat() * 0.01f;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
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
