using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Places a single circular volcanic cone on the island interior.
/// </summary>
public static class VolcanicActivityStage
{
    private const uint StageSalt = 12;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.VolcanicSites.Clear();
        plan.VolcanoExclusion.Zones.Clear();
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float baseRadiusCells = VolcanicConeUtility.ComputeBaseRadiusCells(plan, config);
        int border = Math.Max(0, config.MinOceanBorderCells);

        var candidates = new List<(int X, int Y, float Score)>();
        var fallbackCandidates = new List<(int X, int Y, float Score)>();
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
                float score = cell.Elevation * 2.5f + cell.Moisture * 0.15f;
                score += (1f - distFromCenter) * 0.35f;
                score += NoiseUtility.Fbm(stageSeed + 40, x * 0.07f, y * 0.07f, octaves: 2) * 0.1f;
                fallbackCandidates.Add((x, y, score));
                if (distFromCenter > 0.42f)
                {
                    continue;
                }

                candidates.Add((x, y, score));
            }
        }

        if (candidates.Count == 0)
        {
            candidates = fallbackCandidates;
        }

        if (candidates.Count == 0)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    ref IslandCellData cell = ref plan.GetCell(x, y);
                    if (cell.IsLand)
                    {
                        candidates.Add((x, y, cell.Elevation));
                    }
                }
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        bool placed = false;
        foreach ((int x, int y, _) in candidates)
        {
            float radius = baseRadiusCells * (0.82f + random.NextFloat() * 0.18f);

            var site = new VolcanicSite
            {
                X = x,
                Y = y,
                Origin = VolcanicOrigin.MantlePlume,
                Intensity = config.VolcanicConeHeight,
                RadiusX = radius,
                RadiusY = radius,
                RotationRadians = 0f
            };

            StampVolcanicCone(plan, config, site);
            plan.VolcanicSites.Add(site);
            AddExclusionZone(plan, config, site);
            placed = true;
            break;
        }

        if (!placed && candidates.Count > 0)
        {
            (int x, int y, _) = candidates[0];
            float radius = baseRadiusCells * 0.9f;
            var site = new VolcanicSite
            {
                X = x,
                Y = y,
                Origin = VolcanicOrigin.MantlePlume,
                Intensity = config.VolcanicConeHeight,
                RadiusX = radius,
                RadiusY = radius,
                RotationRadians = 0f
            };

            StampVolcanicCone(plan, config, site);
            plan.VolcanicSites.Add(site);
            AddExclusionZone(plan, config, site);
        }
    }

    private static void AddExclusionZone(IslandPlan plan, IslandDefinition config, VolcanicSite site)
    {
        plan.VolcanoExclusion.Zones.Add(new VolcanoExclusionZone
        {
            CenterX = site.X,
            CenterY = site.Y,
            RadiusX = site.RadiusX,
            RadiusY = site.RadiusY,
            RotationRadians = site.RotationRadians,
            ProtectedRadius = Math.Max(VolcanicConeUtility.LavaCoreRadiusFraction, config.VolcanoProtectedCoreRadius)
        });
    }

    private static void StampVolcanicCone(IslandPlan plan, IslandDefinition config, VolcanicSite site)
    {
        const float apronExtent = VolcanicConeUtility.ApronExtent;
        float apronRadius = site.RadiusX * apronExtent;
        int bound = (int)MathF.Ceiling(apronRadius) + 1;
        float footElevation = ComputeFootElevation(plan, site, apronExtent);

        for (int dy = -bound; dy <= bound; dy++)
        {
            for (int dx = -bound; dx <= bound; dx++)
            {
                int x = site.X + dx;
                int y = site.Y + dy;
                if (!plan.Contains(x, y))
                {
                    continue;
                }

                float norm = VolcanicConeUtility.ComputeNormalizedDistance(site, x, y);
                if (norm > apronExtent)
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand)
                {
                    continue;
                }

                float targetElevation = VolcanicConeUtility.EvaluateElevationProfile(
                    footElevation,
                    config.VolcanicConeHeight,
                    norm);
                cell.Elevation = Math.Clamp(targetElevation, 0f, 1.35f);

                float activity = norm <= VolcanicConeUtility.LavaCoreRadiusFraction
                    ? 0.85f
                    : norm <= VolcanicConeUtility.MountainRingRadiusFraction
                        ? 0.45f
                        : norm <= VolcanicConeUtility.HillRingRadiusFraction
                            ? 0.18f
                            : 0.05f;
                cell.VolcanicActivity = MathF.Max(cell.VolcanicActivity, activity);
            }
        }
    }

    private static float ComputeFootElevation(IslandPlan plan, VolcanicSite site, float apronExtent)
    {
        int bound = (int)MathF.Ceiling(site.RadiusX * (apronExtent + 0.15f)) + 1;
        float sum = 0f;
        int count = 0;

        for (int dy = -bound; dy <= bound; dy++)
        {
            for (int dx = -bound; dx <= bound; dx++)
            {
                int x = site.X + dx;
                int y = site.Y + dy;
                if (!plan.Contains(x, y) || !plan.IsLand(x, y))
                {
                    continue;
                }

                float norm = VolcanicConeUtility.ComputeNormalizedDistance(site, x, y);
                if (norm < apronExtent || norm > apronExtent + 0.15f)
                {
                    continue;
                }

                sum += plan.GetCell(x, y).Elevation;
                count++;
            }
        }

        return count > 0 ? sum / count : plan.GetCell(site.X, site.Y).Elevation;
    }

}
