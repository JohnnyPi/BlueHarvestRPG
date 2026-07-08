using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Places a small number of discrete volcanic cones on the island interior.
/// </summary>
public static class VolcanicActivityStage
{
    private const uint StageSalt = 12;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.VolcanicSites.Clear();
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        int coneCount = Math.Clamp(config.VolcanicConeCount, 1, 3);
        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float coneRadiusCells = Math.Max(3f, maxRadius * config.VolcanicConeRadius);
        int border = Math.Max(0, config.MinOceanBorderCells);
        int minSpacing = (int)(coneRadiusCells * 3.5f);

        var candidates = new List<(int X, int Y, float Score)>();
        for (int y = border; y < plan.Height - border; y++)
        {
            for (int x = border; x < plan.Width - border; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast)
                {
                    continue;
                }

                float distFromCenter = MathF.Sqrt(
                    ((x - centerX) / maxRadius) * ((x - centerX) / maxRadius) +
                    ((y - centerY) / maxRadius) * ((y - centerY) / maxRadius));
                if (distFromCenter > 0.62f)
                {
                    continue;
                }

                float score = cell.Elevation * 1.5f + cell.Moisture * 0.2f;
                score += NoiseUtility.Fbm(stageSeed + 40, x * 0.07f, y * 0.07f, octaves: 2) * 0.15f;
                candidates.Add((x, y, score));
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var placed = new List<(int X, int Y)>();

        foreach ((int x, int y, _) in candidates)
        {
            if (placed.Count >= coneCount)
            {
                break;
            }

            bool tooClose = placed.Any(site =>
                Math.Abs(site.X - x) + Math.Abs(site.Y - y) < minSpacing);
            if (tooClose)
            {
                continue;
            }

            placed.Add((x, y));
            StampVolcanicCone(plan, config, x, y, coneRadiusCells);
            plan.VolcanicSites.Add(new VolcanicSite
            {
                X = x,
                Y = y,
                Origin = VolcanicOrigin.MantlePlume,
                Intensity = config.VolcanicConeHeight
            });
        }
    }

    private static void StampVolcanicCone(
        IslandPlan plan,
        IslandDefinition config,
        int centerX,
        int centerY,
        float radiusCells)
    {
        int radius = Math.Max(3, (int)MathF.Ceiling(radiusCells));
        float radiusSq = radius * radius;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                if (!plan.Contains(x, y))
                {
                    continue;
                }

                float distSq = dx * dx + dy * dy;
                if (distSq > radiusSq)
                {
                    continue;
                }

                float dist = MathF.Sqrt(distSq) / radius;
                float cone = 1f - dist;
                cone *= cone;

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand)
                {
                    continue;
                }

                float uplift = config.VolcanicConeHeight * cone;
                cell.Elevation = Math.Clamp(cell.Elevation + uplift, 0f, 1.25f);
                cell.VolcanicActivity = MathF.Max(cell.VolcanicActivity, uplift * 0.85f);
            }
        }
    }
}
