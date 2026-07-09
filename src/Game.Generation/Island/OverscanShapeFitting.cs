using Game.Content.Definitions;

namespace Game.Generation.Island;

public static class OverscanShapeFitting
{
  public static float ComputeSafeNormalizedHalfExtent(int overscanSize, int cropSize, int marginFromCropEdge)
  {
    if (overscanSize <= 1 || cropSize <= 0)
    {
      return 1f;
    }

    int offset = (overscanSize - cropSize) / 2;
    int safeSource = offset + cropSize - 1 - marginFromCropEdge;
    return (safeSource / (float)(overscanSize - 1)) * 2f - 1f;
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
      extent = MathF.Max(extent, cx + rx);
      extent = MathF.Max(extent, cy + ry);
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
    IslandDefinition config)
  {
    OceanFrameDefinition frame = config.OceanFrame;
    float safeHalfX = ComputeSafeNormalizedHalfExtent(
      overscanWidth,
      cropWidth,
      frame.MinLandDistanceFromEdge);
    float safeHalfY = ComputeSafeNormalizedHalfExtent(
      overscanHeight,
      cropHeight,
      frame.MinLandDistanceFromEdge);
    float safeHalf = MathF.Min(safeHalfX, safeHalfY);

    float authoringExtent = EstimateAuthoringExtent(config);
    float warpPadding = (
        config.IslandShape.DomainWarp.LobingAmplitude
        + config.IslandShape.DomainWarp.Amplitude
        + config.IslandShape.DomainWarp.LargeStrength
        + config.IslandShape.DomainWarp.MediumStrength
        + config.IslandShape.DomainWarp.SmallStrength) * 1.5f;
    float requiredExtent = authoringExtent + warpPadding;

    return Math.Clamp(safeHalf / MathF.Max(0.5f, requiredExtent), 0.45f, 1f);
  }
}
