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
        MainIslandRadius = 1.05f,
        LandElevationThreshold = 0.15f,
        MinOceanBorderCells = 4,
        PaddockCount = 2,
        DockCount = 1,
        HelipadCount = 1,
        HotelCount = 1,
        RestaurantCount = 1,
        AttractionCount = 1,
        MaintenanceAreaCount = 1,
        RuinCount = 1,
        FortificationCount = 1,
        SatelliteIslandCount = 4,
        RiverCount = 3,
        RiverMinElevation = 0.45f,
        RiverHeadSpacing = 8,
        MaxWetBiomeShare = 0.35f,
        MinElevationStdDev = 0.06f
    };

    public static readonly IslandDefinition FullIsland = new()
    {
        OverworldSize = 128,
        RegionCount = 24,
        MainIslandRadius = 1.15f,
        LandElevationThreshold = 0.20f,
        MinOceanBorderCells = 2,
        SatelliteIslandCount = 6,
        SatelliteMaxRadius = 0.10f,
        RiverCount = 6,
        RiverMinElevation = 0.45f,
        RiverHeadSpacing = 12,
        MaxWetBiomeShare = 0.32f,
        MinElevationStdDev = 0.08f
    };
}
