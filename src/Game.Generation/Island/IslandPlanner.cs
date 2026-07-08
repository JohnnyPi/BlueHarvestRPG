using Game.Simulation.World.Island;
using Game.Content.Definitions;
using Game.Generation.Island.Stages;

namespace Game.Generation.Island;

public sealed class IslandPlanner
{
    private readonly IslandDefinition _config;
    private readonly BiomeRulesDefinition _biomeRules;
    private readonly StructureBlueprintCatalog _blueprintCatalog;
    private readonly GenerationDiagnosticsOptions _diagnostics;

    public IslandPlanner(
        IslandDefinition config,
        StructureBlueprintCatalog? blueprintCatalog = null,
        BiomeRulesDefinition? biomeRules = null,
        GenerationDiagnosticsOptions? diagnostics = null)
    {
        _config = config;
        _biomeRules = biomeRules ?? new BiomeRulesDefinition();
        _blueprintCatalog = blueprintCatalog ?? StructureBlueprintCatalogDefaults.Create();
        _diagnostics = diagnostics ?? new GenerationDiagnosticsOptions();
    }

    public IslandPlan Generate(int width, int height, ulong seed)
    {
        IslandPlan plan = CreatePlanWithMask(width, height, seed);

        RunStage("Voronoi regions", () => VoronoiRegionStage.Execute(plan, _config, seed));
        RunStage("Tectonic plates", () => TectonicPlateSetupStage.Execute(plan, _config, seed));
        RunStage("Landmass", () => LandmassStage.Execute(plan, _config, seed));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterLandmassExecute);

        RunStage("Tectonic boundaries", () => TectonicBoundaryStage.Execute(plan, _config, seed));
        RunStage("Landmass reconcile", () => LandmassStage.Reconcile(plan, _config));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterReconcile1);

        RunStage("Volcanic activity", () => VolcanicActivityStage.Execute(plan, _config, seed));
        RunStage("Erosion", () => ErosionStage.Execute(plan, _config, seed));
        RunStage("Landmass reconcile", () => LandmassStage.Reconcile(plan, _config));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterReconcile2);

        RunStage("Land connectivity", () => LandConnectivityStage.Execute(plan, _config));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterLandConnectivity);

        RunStage("Coast distance", () => CoastDistanceStage.Execute(plan, _config));
        RunStage("Derived fields", () => DerivedFieldsStage.Execute(plan, _config));
        RunStage("Coastal landforms", () => CoastalLandformStage.Execute(plan, _config));
        RunStage("Bathymetry", () => BathymetryStage.Execute(plan, _config, seed));

        RunStage("Region biomes", () => RegionBiomeStage.Execute(plan, _config, _biomeRules, seed));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterRegionBiome);

        RunStage("Biome coherence", () => BiomeCoherenceStage.Execute(plan, _config));

        RunStage("Road network", () => RoadNetworkStage.Execute(plan, _config, seed));
        RunStage("Park layout", () => ParkLayoutStage.Execute(plan, _config, seed));
        RunStage("Paddocks", () => PaddockStage.Execute(plan, _config, seed));
        RunStage("Maintenance yards", () => MaintenanceStage.Execute(plan, _config, seed));
        RunStage("Ruins", () => RuinsStage.Execute(plan, _config, seed));
        RunStage("Tunnels", () => TunnelStage.Execute(plan, _config, seed));
        RunStage("Biome finalize", () => BiomeFinalizeStage.Execute(plan, seed));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterBiomeFinalize);

        RunStage("Island balance", () => IslandBalanceStage.Execute(plan, _config, seed));
        CaptureSnapshot(plan, IslandGenerationCheckpoint.AfterIslandBalance);

        RunStage("Biome coherence (final)", () => BiomeCoherenceStage.Execute(plan, _config, finalPass: true));
        RunStage("Bathymetry (final)", () => BathymetryStage.Execute(plan, _config, seed));
        RunStage("Ocean sanitize", () => LandConnectivityStage.SanitizeOceanCells(plan));
        RunStage("Structure finalize", () => StructureFinalizeStage.Execute(plan, seed, _blueprintCatalog));

        if (_diagnostics.RunQualityGate)
        {
            RunStage("Quality gate", () => RunQualityGate(plan));
        }

        return plan;
    }

    private IslandPlan CreatePlanWithMask(int width, int height, ulong seed)
    {
        if (ShouldUseOverscan())
        {
            float scale = Math.Max(1.01f, _config.OceanFrame.OverscanScale);
            int overscanW = (int)MathF.Round(width * scale);
            int overscanH = (int)MathF.Round(height * scale);
            var overscanPlan = new IslandPlan(overscanW, overscanH, seed);

            bool valid = MaskQualityStage.TryGenerateValidMask(
                overscanPlan,
                _config,
                width,
                height,
                seed,
                out _,
                _diagnostics.Progress);

            CaptureSnapshot(overscanPlan, IslandGenerationCheckpoint.AfterMask);
            CaptureSnapshot(overscanPlan, IslandGenerationCheckpoint.AfterCoastlineVariation);

            IslandPlan plan = RunStage("Crop center", () => PlanCropUtility.CropCenter(overscanPlan, width, height));
            plan.OceanFrameValidated = valid;
            plan.GenerationSnapshots = overscanPlan.GenerationSnapshots;
            return plan;
        }

        var directPlan = new IslandPlan(width, height, seed);
        RunStage("Island mask", () => IslandMaskStage.Execute(directPlan, _config, seed));
        CaptureSnapshot(directPlan, IslandGenerationCheckpoint.AfterMask);

        RunStage("Coast distance", () => CoastDistanceStage.Execute(directPlan, _config));
        RunStage("Coastline cleanup", () => CoastlineCleanupStage.Execute(directPlan, _config));
        RunStage("Coastline variation", () => CoastlineVariationStage.Execute(directPlan, _config, seed));
        CaptureSnapshot(directPlan, IslandGenerationCheckpoint.AfterCoastlineVariation);

        return directPlan;
    }

    private bool ShouldUseOverscan()
        => !_config.UseLegacyIslandMask && _config.OceanFrame.OverscanScale > 1.01f;

    private void RunStage(string name, Action action)
    {
        if (_diagnostics.Progress is IslandGenerationProgressReporter progress)
        {
            progress.RunStage(name, action);
            return;
        }

        action();
    }

    private T RunStage<T>(string name, Func<T> action)
    {
        if (_diagnostics.Progress is IslandGenerationProgressReporter progress)
        {
            T result = default!;
            progress.RunStage(name, () => result = action());
            return result;
        }

        return action();
    }

    private void CaptureSnapshot(IslandPlan plan, IslandGenerationCheckpoint checkpoint)
    {
        if (!_diagnostics.CaptureSnapshots)
        {
            return;
        }

        plan.GenerationSnapshots.Add(IslandGenerationSnapshot.Capture(plan, checkpoint));
    }

    private static void RunQualityGate(IslandPlan plan)
    {
        IslandQualityReport report = IslandQualityMetrics.Analyze(plan);
        if (report.LandCellsInForbiddenEdgeBand > 0
            || report.CoastCellsInForbiddenEdgeBand > 0
            || report.BiomeSingletonCount > 2)
        {
            throw new InvalidOperationException(
                $"Island quality gate failed: landEdge={report.LandCellsInForbiddenEdgeBand}, "
                + $"coastEdge={report.CoastCellsInForbiddenEdgeBand}, "
                + $"singletons={report.BiomeSingletonCount}.");
        }
    }
}
