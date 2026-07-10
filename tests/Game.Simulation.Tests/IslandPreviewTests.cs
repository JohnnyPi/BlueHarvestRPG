using System.Reflection;
using Game.Content;
using Game.Content.Definitions;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class IslandPreviewTests
{
    private static readonly string[] IslandParameterNames =
    [
        nameof(IslandDefinition.UseLegacyIslandMask),
        nameof(IslandDefinition.ShelfWidth),
        nameof(IslandDefinition.ShelfDepth),
        nameof(IslandDefinition.DeepOceanDepth),
        nameof(IslandDefinition.DeepOceanWidth),
        nameof(IslandDefinition.MinBeachCoastDistance),
        nameof(IslandDefinition.MaxBeachCoastDistance),
        nameof(IslandDefinition.MinShallowWaterCoastDistance),
        nameof(IslandDefinition.MaxShallowWaterCoastDistance),
        nameof(IslandDefinition.CoastalWidthVariationFrequency),
        nameof(IslandDefinition.CoastalWidthSmoothingPasses),
        nameof(IslandDefinition.InlandCoastDistance),
        nameof(IslandDefinition.LandCoastThreshold),
        nameof(IslandDefinition.CoastalRampStrength),
        nameof(IslandDefinition.VolcanicDomeStrength),
        nameof(IslandDefinition.DetailNoiseWeight),
        nameof(IslandDefinition.RidgeNoiseWeight),
        nameof(IslandDefinition.SeaLevel),
        nameof(IslandDefinition.OverworldSize),
        nameof(IslandDefinition.RegionCount),
        nameof(IslandDefinition.MinOceanBorderCells),
        nameof(IslandDefinition.MinLandComponentCells),
        nameof(IslandDefinition.MainIslandRadius),
        nameof(IslandDefinition.MainIslandElongation),
        nameof(IslandDefinition.MainIslandRotation),
        nameof(IslandDefinition.MainIslandCenterOffsetX),
        nameof(IslandDefinition.MainIslandCenterOffsetY),
        nameof(IslandDefinition.MaskInnerRadius),
        nameof(IslandDefinition.MaskOuterRadius),
        nameof(IslandDefinition.MaskNoiseLarge),
        nameof(IslandDefinition.MaskNoiseMedium),
        nameof(IslandDefinition.MaskNoiseFine),
        nameof(IslandDefinition.SatelliteIslandCount),
        nameof(IslandDefinition.SatelliteMinRadius),
        nameof(IslandDefinition.SatelliteMaxRadius),
        nameof(IslandDefinition.LandElevationThreshold),
        nameof(IslandDefinition.HeightMaskWeight),
        nameof(IslandDefinition.HeightLargeNoiseWeight),
        nameof(IslandDefinition.HeightMediumNoiseWeight),
        nameof(IslandDefinition.HeightFineNoiseWeight),
        nameof(IslandDefinition.HeightVoronoiRidgeWeight),
        nameof(IslandDefinition.WarpLargeStrength),
        nameof(IslandDefinition.WarpMediumStrength),
        nameof(IslandDefinition.WarpSmallStrength),
        nameof(IslandDefinition.BiomeBlendPower),
        nameof(IslandDefinition.BiomeBlendNeighborCount),
        nameof(IslandDefinition.PlateMotionMin),
        nameof(IslandDefinition.PlateMotionMax),
        nameof(IslandDefinition.ContinentalCrustBias),
        nameof(IslandDefinition.SubductionUplift),
        nameof(IslandDefinition.CollisionUplift),
        nameof(IslandDefinition.DivergentRidgeBoost),
        nameof(IslandDefinition.ConvergenceThreshold),
        nameof(IslandDefinition.MantlePlumeCount),
        nameof(IslandDefinition.MantlePlumeRadius),
        nameof(IslandDefinition.MantlePlumeIntensity),
        nameof(IslandDefinition.VolcanicConeCount),
        nameof(IslandDefinition.VolcanicConeRadius),
        nameof(IslandDefinition.VolcanicConeHeight),
        nameof(IslandDefinition.VolcanoProtectedCoreRadius),
        nameof(IslandDefinition.VolcanoRoadRingRadius),
        nameof(IslandDefinition.VolcanoRoadRingNodes),
        nameof(IslandDefinition.LavaFlowCount),
        nameof(IslandDefinition.LavaFlowMaxLength),
        nameof(IslandDefinition.LavaFlowWidth),
        nameof(IslandDefinition.LavaFlowMeanderStrength),
        nameof(IslandDefinition.LavaFlowTerminationRadius),
        nameof(IslandDefinition.LavaFlowRoadTraversalPenalty),
        nameof(IslandDefinition.ErosionIterations),
        nameof(IslandDefinition.ErosionStrength),
        nameof(IslandDefinition.RiverCarveDepth),
        nameof(IslandDefinition.RiverCount),
        nameof(IslandDefinition.RiverMinElevation),
        nameof(IslandDefinition.RiverWidth),
        nameof(IslandDefinition.RiverMaxLength),
        nameof(IslandDefinition.RiverHeadSpacing),
        nameof(IslandDefinition.MaxWetBiomeShare),
        nameof(IslandDefinition.MinElevationStdDev),
        nameof(IslandDefinition.BalancePassMaxIterations),
        nameof(IslandDefinition.DockCount),
        nameof(IslandDefinition.HelipadCount),
        nameof(IslandDefinition.HotelCount),
        nameof(IslandDefinition.RestaurantCount),
        nameof(IslandDefinition.AttractionCount),
        nameof(IslandDefinition.PaddockCount),
        nameof(IslandDefinition.MaintenanceAreaCount),
        nameof(IslandDefinition.RuinCount),
        nameof(IslandDefinition.FortificationCount),
        nameof(IslandDefinition.PaddockFenceRadius),
        nameof(IslandDefinition.TunnelCavernRadius),
        nameof(IslandDefinition.RoadNetworkJunctionCount),
        nameof(IslandDefinition.RoadWidth),
        nameof(IslandDefinition.UseLegacyRandomRoads),
    ];

    private static readonly string[] BiomeParameterNames =
    [
        nameof(BiomeRulesDefinition.OceanMaxElevation),
        nameof(BiomeRulesDefinition.BeachMaxElevation),
        nameof(BiomeRulesDefinition.MountainsMinElevation),
        nameof(BiomeRulesDefinition.SmallMountainMinElevation),
        nameof(BiomeRulesDefinition.HillsMinElevation),
        nameof(BiomeRulesDefinition.FoothillsMinElevation),
        nameof(BiomeRulesDefinition.SwampMinMoisture),
        nameof(BiomeRulesDefinition.ForestMinMoisture),
    ];

    [Fact]
    public void PreviewParameterCatalog_CoversAllScalarDefinitionProperties()
    {
        var islandProperties = typeof(IslandDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => property.Name)
            .Where(name => name is not (
                nameof(IslandDefinition.IslandShape)
                or nameof(IslandDefinition.Ridges)
                or nameof(IslandDefinition.OceanFrame)
                or nameof(IslandDefinition.BiomeCoherence)
                or nameof(IslandDefinition.BiomeNoise)))
            .ToHashSet(StringComparer.Ordinal);
        var biomeProperties = typeof(BiomeRulesDefinition)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(islandProperties, IslandParameterNames.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(biomeProperties, BiomeParameterNames.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void PreviewGeneration_ProducesFullBrightnessSnapshot()
    {
        var bundle = new ContentLoader().LoadAll();
        var generator = new IslandWorldGenerator(
            TestSaveDefaults.Island,
            bundle.CreateBlueprintCatalog(),
            bundle.BiomeRules);
        Overworld world = generator.Generate(777UL);

        var repository = new InMemoryLocalMapRepository(world, new LocalMapGenerator(bundle.CreateBlueprintCatalog()));
        var session = new GameSession(world, repository);
        OverworldExploration.RevealAll(world);
        session.RevealEntireOverworld();
        session.ViewMode = GameViewMode.Overworld;

        var host = new SimulationHost(world, session, repository) { IsNewGame = true };
        host.Initialize();
        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.True(snapshot.DebugFullBrightness);
        Assert.NotNull(snapshot.ExploredTiles);
        Assert.All(snapshot.ExploredTiles, Assert.True);
        Assert.NotNull(snapshot.OverworldLandmarks);
        Assert.NotEmpty(snapshot.OverworldLandmarks);
    }
}
