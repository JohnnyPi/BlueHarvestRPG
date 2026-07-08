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
        float baseRadiusCells = VolcanicConeUtility.ComputeBaseRadiusCells(plan, config);
        int border = Math.Max(0, config.MinOceanBorderCells);
        int minSpacing = (int)(baseRadiusCells * 4.5f);

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
                if (distFromCenter > 0.58f)
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

            float aspect = 1.55f + random.NextFloat() * 0.95f;
            float radiusX = baseRadiusCells * (0.78f + random.NextFloat() * 0.22f);
            float radiusY = radiusX * aspect;
            float rotation = random.NextFloat() * MathF.Tau;

            var site = new VolcanicSite
            {
                X = x,
                Y = y,
                Origin = VolcanicOrigin.MantlePlume,
                Intensity = config.VolcanicConeHeight,
                RadiusX = radiusX,
                RadiusY = radiusY,
                RotationRadians = rotation
            };

            StampVolcanicCone(plan, config, site);
            plan.VolcanicSites.Add(site);
        }
    }

    private static void StampVolcanicCone(IslandPlan plan, IslandDefinition config, VolcanicSite site)
    {
        int boundX = (int)MathF.Ceiling(site.RadiusX) + 1;
        int boundY = (int)MathF.Ceiling(site.RadiusY) + 1;
        float cos = MathF.Cos(site.RotationRadians);
        float sin = MathF.Sin(site.RotationRadians);

        for (int dy = -boundY; dy <= boundY; dy++)
        {
            for (int dx = -boundX; dx <= boundX; dx++)
            {
                int x = site.X + dx;
                int y = site.Y + dy;
                if (!plan.Contains(x, y))
                {
                    continue;
                }

                float localX = dx * cos + dy * sin;
                float localY = -dx * sin + dy * cos;
                float norm = MathF.Sqrt(
                    (localX / site.RadiusX) * (localX / site.RadiusX) +
                    (localY / site.RadiusY) * (localY / site.RadiusY));
                if (norm > 1f)
                {
                    continue;
                }

                float falloff = 1f - norm;
                falloff *= falloff;

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand)
                {
                    continue;
                }

                float heightScale;
                float activity;
                if (norm <= VolcanicConeUtility.LavaCoreRadiusFraction)
                {
                    heightScale = 1.15f;
                    activity = 0.8f + falloff * 0.2f;
                }
                else if (norm <= VolcanicConeUtility.MountainRingRadiusFraction)
                {
                    heightScale = 0.95f;
                    activity = 0.35f + falloff * 0.25f;
                }
                else if (norm <= VolcanicConeUtility.HillRingRadiusFraction)
                {
                    heightScale = 0.55f;
                    activity = 0.12f + falloff * 0.12f;
                }
                else
                {
                    heightScale = 0.22f;
                    activity = 0.04f + falloff * 0.06f;
                }

                float uplift = config.VolcanicConeHeight * falloff * heightScale;
                cell.Elevation = Math.Clamp(cell.Elevation + uplift, 0f, 1.35f);
                cell.VolcanicActivity = MathF.Max(cell.VolcanicActivity, activity);
            }
        }
    }
}
