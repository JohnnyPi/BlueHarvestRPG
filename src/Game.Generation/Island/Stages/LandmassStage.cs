using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Island.Fields;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class LandmassStage
{
    private const uint StageSalt = 2;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        if (config.UseLegacyIslandMask)
        {
            ExecuteLegacy(plan, config, seed);
            return;
        }

        ExecuteFieldDriven(plan, config, seed);
    }

    private static void ExecuteFieldDriven(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        int width = plan.Width;
        int height = plan.Height;
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float landThreshold = config.LandElevationThreshold;
        float outerRadius = config.MaskOuterRadius > 0f ? config.MaskOuterRadius : config.MainIslandRadius;

        var regionById = plan.Regions.ToDictionary(region => region.Id);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float nx = x / (float)Math.Max(1, width - 1);
                float ny = y / (float)Math.Max(1, height - 1);
                float px = nx * 2f - 1f;
                float py = ny * 2f - 1f;

                (float wx, float wy) = NoiseUtility.DomainWarp(
                    stageSeed,
                    nx,
                    ny,
                    config.WarpLargeStrength,
                    config.WarpMediumStrength,
                    config.WarpSmallStrength);

                float coastDistance = plan.CoastDistance.Length > index ? plan.CoastDistance[index] : 0f;
                float coastalRamp = NoiseUtility.SmoothStep(
                    config.BeachCoastDistance,
                    config.InlandCoastDistance,
                    coastDistance) * config.CoastalRampStrength;

                float domeCenterX = -0.05f;
                float domeCenterY = 0.08f;
                float domeDx = px - domeCenterX;
                float domeDy = py - domeCenterY;
                float domeDistSq = domeDx * domeDx + domeDy * domeDy;
                float volcanicDome = MathF.Exp(-domeDistSq / 0.35f) * config.VolcanicDomeStrength;

                float ridgeHeight = RidgeSplineField.Sample(px, py, config.Ridges);
                float detailNoise = NoiseUtility.Fbm(stageSeed + 2, wx * 3f, wy * 3f, octaves: 4);
                float ridgeNoise = NoiseUtility.RidgedNoise(stageSeed + 3, wx * 7f, wy * 7f, octaves: 3);

                float elevation =
                    config.SeaLevel
                    + coastalRamp
                    + volcanicDome
                    + ridgeHeight
                    + detailNoise * config.DetailNoiseWeight
                    + ridgeNoise * config.RidgeNoiseWeight;

                if (coastDistance <= config.LandCoastThreshold)
                {
                    elevation = MathF.Min(elevation, landThreshold - 0.02f);
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.Elevation = elevation;

                float moisture = NoiseUtility.Fbm(stageSeed + 5, wx * 4f, wy * 4f, octaves: 4);
                moisture += NoiseUtility.SmoothStep(0.05f, 0.25f, coastDistance) * 0.12f;
                moisture -= NoiseUtility.SmoothStep(0f, 0.04f, coastDistance) * 0.08f;

                float latitude = 1f - MathF.Abs(ny * 2f - 1f);
                float temperature = Math.Clamp(
                    latitude * 0.6f + NoiseUtility.Fbm(stageSeed + 6, wx * 5f, wy * 5f, octaves: 3) * 0.4f - elevation * 0.25f,
                    0f,
                    1f);

                int regionId = plan.GetRegionId(x, y);
                if (regionById.TryGetValue(regionId, out IslandRegion? region))
                {
                    moisture += region.IsContinental ? 0.03f : -0.03f;
                    temperature += region.IsContinental ? 0.02f : -0.02f;
                }

                cell.Moisture = Math.Clamp(moisture, 0f, 1f);
                cell.Temperature = Math.Clamp(temperature, 0f, 1f);
                cell.IsLand = coastDistance > config.LandCoastThreshold && elevation > landThreshold;
                cell.Biome = cell.IsLand ? BiomeId.Plains : BiomeId.Ocean;
            }
        }

        MarkCoastline(plan, config);
        MarkSatelliteRegions(plan, centerX, centerY, maxRadius, outerRadius);
    }

    private static void ExecuteLegacy(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        int width = plan.Width;
        int height = plan.Height;
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float landThreshold = config.LandElevationThreshold;
        float outerRadius = config.MaskOuterRadius > 0f ? config.MaskOuterRadius : config.MainIslandRadius;

        var regionById = plan.Regions.ToDictionary(region => region.Id);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float nx = x / (float)Math.Max(1, width - 1);
                float ny = y / (float)Math.Max(1, height - 1);

                (float wx, float wy) = NoiseUtility.DomainWarp(
                    stageSeed,
                    nx,
                    ny,
                    config.WarpLargeStrength,
                    config.WarpMediumStrength,
                    config.WarpSmallStrength);

                float islandMask = plan.IslandMask.Length > index ? plan.IslandMask[index] : 0f;
                float largeNoise = NoiseUtility.Fbm(stageSeed + 2, wx * 3f, wy * 3f, octaves: 4);
                float mediumNoise = NoiseUtility.Fbm(stageSeed + 3, wx * 8f, wy * 8f, octaves: 3);
                float fineNoise = NoiseUtility.Fbm(stageSeed + 4, wx * 18f, wy * 18f, octaves: 2);
                float voronoiRidge = plan.VoronoiEdge.Length > index ? plan.VoronoiEdge[index] : 0f;

                float elevation =
                    islandMask * config.HeightMaskWeight +
                    largeNoise * config.HeightLargeNoiseWeight +
                    mediumNoise * config.HeightMediumNoiseWeight +
                    fineNoise * config.HeightFineNoiseWeight +
                    voronoiRidge * config.HeightVoronoiRidgeWeight;

                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.Elevation = elevation;

                float moisture = NoiseUtility.Fbm(stageSeed + 5, wx * 4f, wy * 4f, octaves: 4);
                float latitude = 1f - MathF.Abs(ny * 2f - 1f);
                float temperature = Math.Clamp(
                    latitude * 0.6f + NoiseUtility.Fbm(stageSeed + 6, wx * 5f, wy * 5f, octaves: 3) * 0.4f - elevation * 0.25f,
                    0f,
                    1f);

                int regionId = plan.GetRegionId(x, y);
                if (regionById.TryGetValue(regionId, out IslandRegion? region))
                {
                    moisture += region.IsContinental ? 0.03f : -0.03f;
                    temperature += region.IsContinental ? 0.02f : -0.02f;
                }

                cell.Moisture = Math.Clamp(moisture, 0f, 1f);
                cell.Temperature = Math.Clamp(temperature, 0f, 1f);
                cell.IsLand = elevation > landThreshold;
                cell.Biome = cell.IsLand ? BiomeId.Plains : BiomeId.Ocean;
            }
        }

        MarkCoastline(plan, config);
        MarkSatelliteRegions(plan, centerX, centerY, maxRadius, outerRadius);
    }

    public static void Reconcile(IslandPlan plan, IslandDefinition config)
    {
        float landThreshold = config.LandElevationThreshold;
        int border = Math.Max(0, config.MinOceanBorderCells);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.Elevation = Math.Clamp(cell.Elevation + cell.TectonicUplift, -1f, 1.25f);
                if (!plan.OceanFrameValidated)
                {
                    IslandBorderUtility.ClampElevationInBorderBand(
                        ref cell.Elevation,
                        x,
                        y,
                        plan.Width,
                        plan.Height,
                        border,
                        landThreshold);
                }

                if (!config.UseLegacyIslandMask)
                {
                    int index = y * plan.Width + x;
                    float coastDistance = plan.CoastDistance.Length > index ? plan.CoastDistance[index] : 0f;
                    cell.IsLand = coastDistance > config.LandCoastThreshold && cell.Elevation > landThreshold;
                }
                else
                {
                    cell.IsLand = cell.Elevation > landThreshold;
                }

                cell.IsCoast = false;
                cell.Role &= ~IslandCellRole.Coast;
                if (!cell.IsLand && cell.Biome is not (BiomeId.ShallowWater or BiomeId.Reef))
                {
                    cell.Biome = BiomeId.Ocean;
                }
            }
        }

        MarkCoastline(plan, config);

        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        float outerRadius = config.MaskOuterRadius > 0f ? config.MaskOuterRadius : config.MainIslandRadius;
        MarkSatelliteRegions(plan, centerX, centerY, maxRadius, outerRadius);
    }

    public static void MarkCoastline(IslandPlan plan)
    {
        MarkCoastline(plan, config: null);
    }

    public static void MarkCoastline(IslandPlan plan, IslandDefinition? config)
    {
        bool useCoastDistance = config is not null
            && !config.UseLegacyIslandMask
            && plan.CoastDistance.Length == plan.Width * plan.Height;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                bool isCoast;
                if (useCoastDistance)
                {
                    int index = y * plan.Width + x;
                    float coastDistance = plan.CoastDistance[index];
                    isCoast = coastDistance <= config!.InlandCoastDistance;
                }
                else
                {
                    isCoast =
                        !plan.Contains(x - 1, y) || !plan.IsLand(x - 1, y) ||
                        !plan.Contains(x + 1, y) || !plan.IsLand(x + 1, y) ||
                        !plan.Contains(x, y - 1) || !plan.IsLand(x, y - 1) ||
                        !plan.Contains(x, y + 1) || !plan.IsLand(x, y + 1);
                }

                if (isCoast)
                {
                    ref IslandCellData cell = ref plan.GetCell(x, y);
                    cell.IsCoast = true;
                    cell.Role |= IslandCellRole.Coast;
                    cell.Biome = BiomeId.Beach;
                }
            }
        }
    }

    private static void MarkSatelliteRegions(
        IslandPlan plan,
        float centerX,
        float centerY,
        float maxRadius,
        float mainRadius)
    {
        float mainThreshold = maxRadius * mainRadius * 0.9f;

        foreach (IslandRegion region in plan.Regions)
        {
            float dx = region.SiteX - centerX;
            float dy = region.SiteY - centerY;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            region.IsMainIsland = dist < mainThreshold;

            if (!region.IsMainIsland && plan.IsLand(region.SiteX, region.SiteY))
            {
                region.IsSatelliteIsland = true;
            }
        }
    }
}
