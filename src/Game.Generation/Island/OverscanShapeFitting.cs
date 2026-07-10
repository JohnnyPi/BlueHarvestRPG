using Game.Content.Definitions;

namespace Game.Generation.Island;

public static class OverscanShapeFitting
{
  public static float ComputeSafeNormalizedHalfExtent(
    int overscanSize,
    int cropSize,
    int marginFromCropEdge,
    int cropOffset)
  {
    if (overscanSize <= 1 || cropSize <= 0)
    {
      return 1f;
    }

    int safeMin = cropOffset + marginFromCropEdge;
    int safeMax = cropOffset + cropSize - 1 - marginFromCropEdge;
    float minNormalized = safeMin / (float)(overscanSize - 1) * 2f - 1f;
    float maxNormalized = safeMax / (float)(overscanSize - 1) * 2f - 1f;
    return MathF.Max(0f, MathF.Min(-minNormalized, maxNormalized));
  }

  public static float EstimateAuthoringExtent(IslandDefinition config)
  {
    float extent = 0f;
    IslandShapeDefinition shape = config.IslandShape;

    foreach (IslandBlobDefinition blob in shape.AdditiveBlobs)
    {
      float cx = blob.Center.Length > 0 ? MathF.Abs(blob.Center[0]) : 0f;
      float cy = blob.Center.Length > 1 ? MathF.Abs(blob.Center[1]) : 0f;
      float rx = blob.Radius.Length > 0 ? blob.Radius[0] : 0f;
      float ry = blob.Radius.Length > 1 ? blob.Radius[1] : rx;
      float rotation = blob.RotationDegrees * MathF.PI / 180f;
      float cos = MathF.Cos(rotation);
      float sin = MathF.Sin(rotation);
      float halfX = MathF.Sqrt(rx * rx * cos * cos + ry * ry * sin * sin);
      float halfY = MathF.Sqrt(rx * rx * sin * sin + ry * ry * cos * cos);
      extent = MathF.Max(extent, cx + halfX);
      extent = MathF.Max(extent, cy + halfY);
    }

    if (config.SatelliteIslandCount > 0)
    {
      float satOrbit = 0.84f;
      float satRadius = config.SatelliteMaxRadius;
      extent = MathF.Max(extent, satOrbit + satRadius);
    }

    return extent;
  }

  public static float ComputeInitialShapeScale(
    int overscanWidth,
    int overscanHeight,
    int cropWidth,
    int cropHeight,
    IslandDefinition config,
    int cropOffsetX,
    int cropOffsetY)
  {
    OceanFrameDefinition frame = config.OceanFrame;
    float safeHalfX = ComputeSafeNormalizedHalfExtent(
      overscanWidth,
      cropWidth,
      frame.MinLandDistanceFromEdge,
      cropOffsetX);
    float safeHalfY = ComputeSafeNormalizedHalfExtent(
      overscanHeight,
      cropHeight,
      frame.MinLandDistanceFromEdge,
      cropOffsetY);
    float safeHalf = MathF.Min(safeHalfX, safeHalfY);

    float authoringExtent = EstimateAuthoringExtent(config);
    // Every warp operation adds a positive displacement bounded by its configured
    // amplitude. They are composed sequentially, so the per-axis bound is their sum.
    float warpPadding =
        config.IslandShape.DomainWarp.LobingAmplitude
        + config.IslandShape.DomainWarp.Amplitude
        + config.IslandShape.DomainWarp.LargeStrength
        + config.IslandShape.DomainWarp.MediumStrength
        + config.IslandShape.DomainWarp.SmallStrength;
    float requiredExtent = authoringExtent + warpPadding;

    return Math.Clamp(safeHalf / MathF.Max(0.5f, requiredExtent), 0.40f, 1f);
  }
}
