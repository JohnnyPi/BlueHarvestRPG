using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class LandmassStage
{
  private const uint StageSalt = 2;

  public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
  {
    ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
    var random = new DeterministicRandom(stageSeed);

    int width = plan.Width;
    int height = plan.Height;
    float centerX = (width - 1) * 0.5f;
    float centerY = (height - 1) * 0.5f;
    float maxRadius = Math.Min(centerX, centerY);
    float landThreshold = config.LandElevationThreshold;

    var satelliteCenters = new List<(float X, float Y, float Radius)>();
    int satelliteCount = Math.Clamp(config.SatelliteIslandCount, 0, 24);

    for (int i = 0; i < satelliteCount; i++)
    {
      float angle = random.NextFloat() * MathF.PI * 2f;
      float minDistance = config.MainIslandRadius + 0.03f;
      float maxDistance = 0.97f;
      float distance = minDistance + random.NextFloat() * MathF.Max(0.01f, maxDistance - minDistance);
      float sx = centerX + MathF.Cos(angle) * maxRadius * distance;
      float sy = centerY + MathF.Sin(angle) * maxRadius * distance;
      float radius = maxRadius * (config.SatelliteMinRadius +
                                  random.NextFloat() * (config.SatelliteMaxRadius - config.SatelliteMinRadius));
      satelliteCenters.Add((sx, sy, radius));
    }

    int border = Math.Max(0, config.MinOceanBorderCells);

    for (int y = 0; y < height; y++)
    {
      for (int x = 0; x < width; x++)
      {
        float nx = x / (float)(width - 1);
        float ny = y / (float)(height - 1);

        float warpX = ValueNoise.Sample(stageSeed, nx * 2f, ny * 2f, octaves: 3) * 0.12f;
        float warpY = ValueNoise.Sample(stageSeed + 1, nx * 2f, ny * 2f, octaves: 3) * 0.12f;

        float wx = nx + warpX;
        float wy = ny + warpY;

        float dx = (x - centerX) / maxRadius;
        float dy = (y - centerY) / maxRadius;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float elevationNoise =
            ValueNoise.Sample(stageSeed + 2, wx * 3f, wy * 3f, octaves: 4) * 0.65f +
            ValueNoise.Sample(stageSeed + 3, wx * 8f, wy * 8f, octaves: 3) * 0.35f;

        float mainFalloff = 1f - dist / config.MainIslandRadius;
        mainFalloff = Math.Clamp(mainFalloff, 0f, 1f);
        mainFalloff = MathF.Pow(mainFalloff, 0.75f);

        float elevation = elevationNoise * mainFalloff * 1.15f;

        foreach ((float sx, float sy, float radius) in satelliteCenters)
        {
          float sdx = x - sx;
          float sdy = y - sy;
          float sdist = MathF.Sqrt(sdx * sdx + sdy * sdy) / radius;
          float satFalloff = Math.Clamp(1f - sdist, 0f, 1f);
          satFalloff *= satFalloff;
          float satNoise = ValueNoise.Sample(stageSeed + 4, x * 0.05f, y * 0.05f, octaves: 2);
          elevation = MathF.Max(elevation, satNoise * satFalloff * 0.85f);
        }

        if (x < border || y < border || x >= width - border || y >= height - border)
        {
          elevation = MathF.Min(elevation, landThreshold * 0.5f);
        }

        ref IslandCellData cell = ref plan.GetCell(x, y);
        cell.Elevation = elevation;
        cell.Moisture = ValueNoise.Sample(stageSeed + 5, wx * 4f, wy * 4f, octaves: 4);
        float latitude = 1f - MathF.Abs(ny * 2f - 1f);
        cell.Temperature = Math.Clamp(
            latitude * 0.6f + ValueNoise.Sample(stageSeed + 6, wx * 5f, wy * 5f) * 0.4f - elevation * 0.25f,
            0f,
            1f);

        int regionId = plan.GetRegionId(x, y);
        IslandRegion? region = plan.Regions.FirstOrDefault(r => r.Id == regionId);
        if (region is not null)
        {
          elevation += region.IsContinental ? 0.06f : -0.04f;
          cell.Elevation = elevation;
        }

        cell.IsLand = elevation > landThreshold;
        cell.Biome = cell.IsLand ? BiomeId.Plains : BiomeId.Ocean;
      }
    }

    MarkCoastline(plan);
    MarkSatelliteRegions(plan, centerX, centerY, maxRadius, config.MainIslandRadius);
  }

  public static void Reconcile(IslandPlan plan, IslandDefinition config)
  {
    float landThreshold = config.LandElevationThreshold;

    for (int y = 0; y < plan.Height; y++)
    {
      for (int x = 0; x < plan.Width; x++)
      {
        ref IslandCellData cell = ref plan.GetCell(x, y);
        cell.Elevation = Math.Clamp(cell.Elevation + cell.TectonicUplift, 0f, 1.25f);
        cell.IsLand = cell.Elevation > landThreshold;
        cell.IsCoast = false;
        cell.Role &= ~IslandCellRole.Coast;
        if (!cell.IsLand)
        {
          cell.Biome = BiomeId.Ocean;
        }
      }
    }

    MarkCoastline(plan);

    float centerX = (plan.Width - 1) * 0.5f;
    float centerY = (plan.Height - 1) * 0.5f;
    float maxRadius = Math.Min(centerX, centerY);
    MarkSatelliteRegions(plan, centerX, centerY, maxRadius, config.MainIslandRadius);
  }

  private static void MarkCoastline(IslandPlan plan)
  {
    for (int y = 0; y < plan.Height; y++)
    {
      for (int x = 0; x < plan.Width; x++)
      {
        if (!plan.IsLand(x, y))
        {
          continue;
        }

        bool adjacentOcean =
            !plan.Contains(x - 1, y) || !plan.IsLand(x - 1, y) ||
            !plan.Contains(x + 1, y) || !plan.IsLand(x + 1, y) ||
            !plan.Contains(x, y - 1) || !plan.IsLand(x, y - 1) ||
            !plan.Contains(x, y + 1) || !plan.IsLand(x, y + 1);

        if (adjacentOcean)
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
