using Game.Content;
using Game.Content.Definitions;
using Game.Generation.Biomes;

namespace Game.Simulation.Tests;

internal static class TestSaveDefaults
{
    public static readonly BiomeClassifier Classifier = BiomeClassifier.CreateDefault();
    public static readonly uint RulesHash = BiomeRulesHash.Compute(new BiomeRulesDefinition());
    public static readonly IslandDefinition Island = new()
    {
        OverworldSize = 64,
        RegionCount = 16,
        LandElevationThreshold = 0.18f,
        SeaLevel = 0.35f,
        MinOceanBorderCells = 4,
        VolcanicConeCount = 2,
        PaddockCount = 2,
        DockCount = 1,
        HelipadCount = 1,
        HotelCount = 1,
        RestaurantCount = 1,
        AttractionCount = 1,
        MaintenanceAreaCount = 1,
        RuinCount = 1,
        FortificationCount = 1,
        SatelliteIslandCount = 2,
        RiverCount = 3,
        RiverMinElevation = 0.45f,
        RiverHeadSpacing = 8,
        MaxWetBiomeShare = 0.35f,
        MinElevationStdDev = 0.06f,
        UseLegacyIslandMask = true,
        MainIslandRadius = 0.72f,
        MaskOuterRadius = 0.72f,
        MaskInnerRadius = 0.25f,
    };

    public static readonly IslandDefinition FullIsland = new()
    {
        OverworldSize = 128,
        RegionCount = 24,
        LandElevationThreshold = 0.18f,
        SeaLevel = 0.35f,
        MinOceanBorderCells = 2,
        VolcanicConeCount = 2,
        SatelliteIslandCount = 2,
        SatelliteMaxRadius = 0.10f,
        RiverCount = 6,
        RiverMinElevation = 0.45f,
        RiverHeadSpacing = 12,
        MaxWetBiomeShare = 0.32f,
        MinElevationStdDev = 0.08f,
        UseLegacyIslandMask = true,
        MainIslandRadius = 0.72f,
        MaskOuterRadius = 0.72f,
    };

    public static void ApplyFastOceanFrameForTests(IslandDefinition island)
    {
        island.OceanFrame.OverscanScale = 1.1f;
        island.OceanFrame.MaxRegenerationAttempts = 2;
        island.OceanFrame.MinLandDistanceFromEdge = 16;
        island.OceanFrame.MinCoastDistanceFromEdge = 10;
        island.OceanFrame.MaxAxisAlignedCoastRun = 28;
    }
}
