namespace Game.Content.Definitions;

public sealed class IslandDefinition
{
    /// <summary>
    /// Legacy elliptical mask generation used by fast unit tests and IslandPreview tooling.
    /// Production content leaves this false; do not enable in shipped saves.
    /// </summary>
    public bool UseLegacyIslandMask { get; set; }
    public IslandShapeDefinition IslandShape { get; set; } = IslandShapeDefaults.CreateNublar();
    public List<IslandRidgeDefinition> Ridges { get; set; } = [.. IslandShapeDefaults.CreateNublarRidges()];
    public float ShelfWidth { get; set; } = 0.10f;
    public float ShelfDepth { get; set; } = 0.08f;
    public float DeepOceanDepth { get; set; } = 0.35f;
    public float DeepOceanWidth { get; set; } = 0.45f;
    public float BeachCoastDistance { get; set; } = 0.04f;
    public float InlandCoastDistance { get; set; } = 0.08f;
    public float LandCoastThreshold { get; set; } = 0.01f;
    public float CoastalRampStrength { get; set; } = 0.18f;
    public float VolcanicDomeStrength { get; set; } = 0.14f;
    public float DetailNoiseWeight { get; set; } = 0.08f;
    public float RidgeNoiseWeight { get; set; } = 0.03f;
    public float SeaLevel { get; set; } = 0.35f;

    public int OverworldSize { get; set; } = 512;
    public int RegionCount { get; set; } = 96;
    public float MainIslandRadius { get; set; } = 0.38f;
    public float LandElevationThreshold { get; set; } = 0.35f;
    public int MinOceanBorderCells { get; set; } = 24;
    public int SatelliteIslandCount { get; set; } = 0;
    public int MinLandComponentCells { get; set; } = 9;
    public float MainIslandElongation { get; set; } = 1.5f;
    public float MainIslandRotation { get; set; } = 0.6f;
    public float MainIslandCenterOffsetX { get; set; } = 0.06f;
    public float MainIslandCenterOffsetY { get; set; } = 0.10f;
    public float SatelliteMinRadius { get; set; } = 0.04f;
    public float SatelliteMaxRadius { get; set; } = 0.08f;
    public int DockCount { get; set; } = 3;
    public int HelipadCount { get; set; } = 2;
    public int HotelCount { get; set; } = 2;
    public int RestaurantCount { get; set; } = 3;
    public int AttractionCount { get; set; } = 4;
    public int PaddockCount { get; set; } = 6;
    public int MaintenanceAreaCount { get; set; } = 4;
    public int RuinCount { get; set; } = 5;
    public int FortificationCount { get; set; } = 4;
    public int PaddockFenceRadius { get; set; } = 12;
    public int TunnelCavernRadius { get; set; } = 4;
    public float PlateMotionMin { get; set; } = 0.35f;
    public float PlateMotionMax { get; set; } = 1.0f;
    public float ContinentalCrustBias { get; set; } = 0.62f;
    public float SubductionUplift { get; set; } = 0.14f;
    public float CollisionUplift { get; set; } = 0.28f;
    public float DivergentRidgeBoost { get; set; } = 0.06f;
    public int MantlePlumeCount { get; set; } = 2;
    public float MantlePlumeRadius { get; set; } = 0.04f;
    public float MantlePlumeIntensity { get; set; } = 0.22f;
    public int VolcanicConeCount { get; set; } = 2;
    public float VolcanicConeRadius { get; set; } = 0.045f;
    public float VolcanicConeHeight { get; set; } = 0.35f;
    public float ConvergenceThreshold { get; set; } = 0.12f;
    public int RiverCount { get; set; } = 8;
    public float RiverMinElevation { get; set; } = 0.40f;
    public int RiverWidth { get; set; } = 2;
    public int RiverMaxLength { get; set; } = 200;
    public int RiverHeadSpacing { get; set; } = 24;
    public float MaxWetBiomeShare { get; set; } = 0.32f;
    public float MinElevationStdDev { get; set; } = 0.08f;
    public int BalancePassMaxIterations { get; set; } = 4;
    public OceanFrameDefinition OceanFrame { get; set; } = new();
    public BiomeCoherenceDefinition BiomeCoherence { get; set; } = new();
    public BiomeNoiseDefinition BiomeNoise { get; set; } = new();
    public int RoadNetworkJunctionCount { get; set; } = 5;
    public int RoadWidth { get; set; } = 2;
    /// <summary>
    /// Legacy random road pass retained for IslandPreview comparisons. Off in production content.
    /// </summary>
    public bool UseLegacyRandomRoads { get; set; }

    public float MaskInnerRadius { get; set; } = 0.25f;
    public float MaskOuterRadius { get; set; } = 0.72f;
    public float MaskNoiseLarge { get; set; } = 0.42f;
    public float MaskNoiseMedium { get; set; } = 0.20f;
    public float MaskNoiseFine { get; set; } = 0.08f;

    public float WarpLargeStrength { get; set; } = 0.28f;
    public float WarpMediumStrength { get; set; } = 0.12f;
    public float WarpSmallStrength { get; set; } = 0.05f;

    public float HeightMaskWeight { get; set; } = 0.55f;
    public float HeightLargeNoiseWeight { get; set; } = 0.25f;
    public float HeightMediumNoiseWeight { get; set; } = 0.12f;
    public float HeightFineNoiseWeight { get; set; } = 0.05f;
    public float HeightVoronoiRidgeWeight { get; set; } = 0.04f;

    public float BiomeBlendPower { get; set; } = 2.0f;
    public int BiomeBlendNeighborCount { get; set; } = 3;

    public int ErosionIterations { get; set; } = 3;
    public float ErosionStrength { get; set; } = 0.02f;
    public float RiverCarveDepth { get; set; } = 0.04f;
}
