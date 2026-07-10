using Game.Content;
using Game.Content.Definitions;
using Game.Generation.Island;
using Game.Generation.Island.Stages;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandGenerationQualityTests
{
    private static IslandDefinition CreateProductionLikeConfig() => new()
    {
        OverworldSize = 128,
        RegionCount = 24,
        LandElevationThreshold = 0.35f,
        SeaLevel = 0.35f,
        MinOceanBorderCells = 12,
        MinLandComponentCells = 9,
        SatelliteIslandCount = 0,
        VolcanicConeCount = 2,
        RiverCount = 2,
        DockCount = 1,
        HelipadCount = 1,
        HotelCount = 1,
        RestaurantCount = 1,
        AttractionCount = 1,
        MaintenanceAreaCount = 1,
        RuinCount = 1,
        FortificationCount = 1,
        PaddockCount = 1,
        OceanFrame = new OceanFrameDefinition
        {
            OverscanScale = 1.2f,
            MinLandDistanceFromEdge = 10,
            MinCoastDistanceFromEdge = 6,
            MaxRegenerationAttempts = 4,
            MaxAxisAlignedCoastRun = 24,
            EdgeLinearityBand = 16,
        },
        BiomeCoherence = new BiomeCoherenceDefinition
        {
            MinPatchPlains = 8,
            MinPatchForest = 10,
            MinPatchJungle = 12,
            MinPatchSwamp = 8,
            MinPatchHills = 8,
            MinPatchMountains = 6,
        },
    };

    [Fact]
    public void IslandGenerator_HasNoOrphanLandComponents()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 4242UL);

        int orphanCells = CountOrphanLandCells(plan, config.MinLandComponentCells);
        Assert.Equal(0, orphanCells);
    }

    [Fact]
    public void IslandGenerator_VolcanicBiomeOnlyNearVolcanicActivity()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 9001UL);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.Biome != BiomeId.Volcanic)
                {
                    continue;
                }

                bool nearCone = plan.VolcanicSites.Any(site =>
                    VolcanicConeUtility.IsInsideLavaCore(site, x, y));

                Assert.True(
                    cell.VolcanicActivity > 0f || nearCone,
                    $"Volcanic biome at ({x}, {y}) has no volcanic activity or cone proximity.");
            }
        }
    }

    [Fact]
    public void IslandGenerator_ShallowWaterIsCloserToShoreThanDeepOcean()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 4242UL);

        float shallowAverage = 0f;
        int shallowCount = 0;
        float oceanAverage = 0f;
        int oceanCount = 0;

        for (int i = 0; i < plan.Cells.Length; i++)
        {
            if (plan.Cells[i].IsLand)
            {
                continue;
            }

            float offshore = -plan.CoastDistance[i];
            if (plan.Cells[i].Biome is BiomeId.ShallowWater or BiomeId.Reef)
            {
                shallowAverage += offshore;
                shallowCount++;
            }
            else if (plan.Cells[i].Biome == BiomeId.Ocean)
            {
                oceanAverage += offshore;
                oceanCount++;
            }
        }

        Assert.True(shallowCount > 0);
        Assert.True(oceanCount > 0);
        Assert.True(shallowAverage / shallowCount < oceanAverage / oceanCount);
    }

    [Fact]
    public void IslandGenerator_ModernPath_HasFewBiomeSingletons()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 4242UL);

        IslandQualityReport report = IslandQualityMetrics.Analyze(
            plan,
            edgeLandForbiddenBand: config.OceanFrame.MinLandDistanceFromEdge / 2,
            edgeCoastForbiddenBand: config.OceanFrame.MinCoastDistanceFromEdge / 2,
            edgeLinearityBand: config.OceanFrame.EdgeLinearityBand);

        string singletonDetails = string.Join(
            ", ",
            Enum.GetValues<BiomeId>()
                .SelectMany(biome => IslandQualityMetrics.FindBiomeComponents(plan, biome)
                    .Where(component => component.Count == 1)
                    .Select(component =>
                    {
                        int index = component[0];
                        return $"{biome}@({index % plan.Width},{index / plan.Width})";
                    })));
        Assert.True(
            report.BiomeSingletonCount <= 2,
            $"Expected <=2 singletons, got {report.BiomeSingletonCount}: {singletonDetails}.");
    }

    [Fact]
    public void IslandGenerator_ModernPath_RespectsEdgeLandPadding()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 9001UL);

        int minLandDist = IslandQualityMetrics.MinLandDistanceFromEdge(plan);
        Assert.True(minLandDist >= config.OceanFrame.MinLandDistanceFromEdge / 2,
            $"Land was only {minLandDist} cells from map edge.");
    }

    [Fact]
    public void IslandGenerator_ModernPath_HasNoLongAxisAlignedBorderCoasts()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 1337UL);

        int maxRun = IslandQualityMetrics.MaxAxisAlignedCoastRun(
            plan,
            horizontal: true,
            edgeBand: config.OceanFrame.EdgeLinearityBand);

        Assert.True(maxRun <= config.OceanFrame.MaxAxisAlignedCoastRun,
            $"Horizontal coast run was {maxRun} cells.");
    }

    [Fact]
    public void BiomeCoherenceStage_RemovesTinyPatches()
    {
        var plan = new IslandPlan(16, 16, seed: 1UL);
        var config = CreateProductionLikeConfig();
        for (int i = 0; i < plan.Cells.Length; i++)
        {
            plan.Cells[i].IsLand = true;
            plan.Cells[i].Biome = BiomeId.Plains;
        }

        plan.GetCell(4, 4).Biome = BiomeId.Jungle;
        plan.GetCell(10, 10).Biome = BiomeId.Swamp;

        BiomeCoherenceStage.Execute(plan, config);

        Assert.Equal(BiomeId.Plains, plan.GetCell(4, 4).Biome);
        Assert.Equal(BiomeId.Plains, plan.GetCell(10, 10).Biome);
    }

    [Fact]
    public void MaskQualityStage_RejectsLandTooCloseToCropEdge()
    {
        var config = CreateProductionLikeConfig();
        config.OceanFrame.MinLandDistanceFromEdge = 12;
        var plan = new IslandPlan(32, 32, seed: 1UL);
        plan.IslandMask = new float[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                plan.IslandMask[y * 32 + x] = x >= 8 && x < 24 && y >= 8 && y < 24 ? 1f : 0f;
            }
        }

        MaskQualityResult result = MaskQualityStage.ValidateCropWindow(plan, config, cropWidth: 24, cropHeight: 24);
        Assert.False(result.Passed);
        Assert.True(result.LandViolations > 0);
    }

    [Fact]
    public void MaskQualityStage_SelectsLargestValidScaleForCropWindow()
    {
        var config = CreateProductionLikeConfig();
        config.IslandShape = IslandShapeDefaults.CreateNublar();
        config.OceanFrame = new OceanFrameDefinition
        {
            OverscanScale = 1.30f,
            MinLandDistanceFromEdge = 24,
            MinCoastDistanceFromEdge = 16,
            MaxRegenerationAttempts = 8,
            MaxAxisAlignedCoastRun = 20,
            EdgeLinearityBand = 24,
        };

        const int cropSize = 256;
        int overscanSize = (int)MathF.Round(cropSize * config.OceanFrame.OverscanScale);
        var overscanPlan = new IslandPlan(overscanSize, overscanSize, seed: 4242UL);

        bool valid = MaskQualityStage.TryGenerateValidMask(
            overscanPlan,
            config,
            cropSize,
            cropSize,
            seed: 4242UL,
            out MaskQualityResult result,
            out _,
            out _);

        Assert.True(valid, $"Mask quality failed: land={result.LandViolations}, coast={result.CoastViolations}, run={result.MaxAxisAlignedCoastRun}");
        Assert.Equal(0, result.LandViolations);
        Assert.Equal(0, result.CoastViolations);
    }

    [Fact]
    public void IslandGenerator_ProductionOverscan_ValidatesOceanFrame()
    {
        var config = CreateProductionLikeConfig();
        config.OverworldSize = 256;
        config.RegionCount = 24;
        config.OceanFrame = new OceanFrameDefinition
        {
            OverscanScale = 1.30f,
            MinLandDistanceFromEdge = 24,
            MinCoastDistanceFromEdge = 16,
            MaxRegenerationAttempts = 8,
            MaxAxisAlignedCoastRun = 20,
            EdgeLinearityBand = 24,
        };
        config.IslandShape = IslandShapeDefaults.CreateNublar();

        IslandPlan plan = new IslandPlanner(config).Generate(256, 256, seed: 4242UL);

        Assert.True(plan.OceanFrameValidated, "Overscan mask should select a valid crop-window scale.");
        int minLandDist = IslandQualityMetrics.MinLandDistanceFromEdge(plan);
        Assert.True(minLandDist >= config.OceanFrame.MinLandDistanceFromEdge / 2,
            $"Land was only {minLandDist} cells from map edge.");

        int maxRun = IslandQualityMetrics.MaxAxisAlignedCoastRun(
            plan,
            horizontal: true,
            edgeBand: config.OceanFrame.EdgeLinearityBand);
        Assert.True(maxRun <= config.OceanFrame.MaxAxisAlignedCoastRun,
            $"Horizontal coast run was {maxRun} cells.");
    }

    [Fact]
    public void IslandGenerator_Regression8478919930192148244_HasValidFrameAndVariableBeach()
    {
        const ulong seed = 8478919930192148244UL;
        GameContentBundle bundle = new ContentLoader().LoadAll();
        IslandDefinition config = bundle.Island;
        var progress = new IslandGenerationProgressReporter();
        IslandPlan plan = new IslandPlanner(
            config,
            bundle.CreateBlueprintCatalog(),
            bundle.BiomeRules,
            new GenerationDiagnosticsOptions
            {
                CaptureSnapshots = false,
                Progress = progress,
            }).Generate(config.OverworldSize, config.OverworldSize, seed);
        IslandGenerationDiagnostics diagnostics = plan.GenerationDiagnostics;

        Assert.True(diagnostics.OceanFramePassed,
            $"Frame failed: land={diagnostics.LandFrameViolations}, coast={diagnostics.CoastFrameViolations}, run={diagnostics.MaxAxisAlignedCoastRun}.");
        Assert.NotEmpty(diagnostics.AttemptedShapeScales);
        Assert.InRange(diagnostics.SelectedShapeScale, 0.35f, 1f);
        IslandGenerationProgressSnapshot timing = progress.GetSnapshot();
        string slowestStages = string.Join(
            ", ",
            timing.CompletedStages
                .OrderByDescending(stage => stage.DurationMs)
                .Take(8)
                .Select(stage => $"{stage.Name}={stage.DurationMs:F0}ms"));
        Assert.True(
            diagnostics.CroppedLandCoverage is >= 0.20f and <= 0.22f,
            $"Coverage {diagnostics.CroppedLandCoverage:F3} outside target; slowest stages: {slowestStages}.");
        int minLandX = plan.Cells.Select((cell, index) => (cell, index))
            .Where(item => item.cell.IsLand).Min(item => item.index % plan.Width);
        int maxLandX = plan.Cells.Select((cell, index) => (cell, index))
            .Where(item => item.cell.IsLand).Max(item => item.index % plan.Width);
        int minLandY = plan.Cells.Select((cell, index) => (cell, index))
            .Where(item => item.cell.IsLand).Min(item => item.index / plan.Width);
        int maxLandY = plan.Cells.Select((cell, index) => (cell, index))
            .Where(item => item.cell.IsLand).Max(item => item.index / plan.Width);
        Assert.True(Math.Max(maxLandX - minLandX, maxLandY - minLandY) >= plan.Width * 0.40f,
            "Main landmass diameter did not occupy enough of the crop.");
        Assert.All(
            plan.Cells.Select((cell, index) => (cell, index)).Where(item => item.cell.Biome == BiomeId.Ocean),
            item => Assert.True(plan.ExteriorOcean[item.index], $"Enclosed Ocean at index {item.index}."));

        var observedWidths = plan.Cells
            .Select((cell, index) => (cell, index))
            .Where(item => item.cell.IsLand && item.cell.Biome == BiomeId.Beach)
            .Select(item => plan.BeachWidth[item.index])
            .ToList();
        Assert.NotEmpty(observedWidths);
        Assert.All(observedWidths, width =>
            Assert.InRange(width, config.MinBeachCoastDistance, config.MaxBeachCoastDistance));
        Assert.True(observedWidths.Select(width => MathF.Round(width, 3)).Distinct().Count() >= 3,
            "Expected multiple smoothly varying beach widths.");
        Assert.Contains(
            plan.Cells.Select((cell, index) => (cell, index))
                .Where(item => item.cell.IsLand && item.cell.Biome == BiomeId.Beach),
            item => MathF.Abs(plan.Concavity[item.index]) > 0.0001f);

        for (int i = 0; i < plan.Cells.Length; i++)
        {
            if (plan.Cells[i].IsLand && plan.CoastDistance[i] <= plan.BeachWidth[i])
            {
                Assert.Equal(BiomeId.Beach, plan.Cells[i].Biome);
            }
        }
    }

    [Fact]
    public void OverscanShapeFitting_UsesRequestedCentroidCropOffset()
    {
        float centered = OverscanShapeFitting.ComputeSafeNormalizedHalfExtent(130, 100, 10, cropOffset: 15);
        float shifted = OverscanShapeFitting.ComputeSafeNormalizedHalfExtent(130, 100, 10, cropOffset: 22);

        Assert.True(shifted < centered);
    }

    [Fact]
    public void IslandGenerator_OceanCellsUseOceanBiome()
    {
        var config = CreateProductionLikeConfig();
        IslandPlan plan = new IslandPlanner(config).Generate(128, 128, seed: 1337UL);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand)
                {
                    Assert.True(
                        cell.Biome is BiomeId.Ocean or BiomeId.ShallowWater or BiomeId.Reef,
                        $"Unexpected ocean biome {cell.Biome} at ({x}, {y}).");
                    Assert.False(cell.IsCoast);
                }
            }
        }
    }

    [Fact]
    public void LandConnectivityStage_KeepsConfiguredSatelliteComponents()
    {
        var plan = new IslandPlan(64, 64, seed: 1UL);
        var config = new IslandDefinition
        {
            LandElevationThreshold = 0.18f,
            MinLandComponentCells = 9,
            SatelliteIslandCount = 2,
            SatelliteMinRadius = 0.04f
        };

        StampLandBlob(plan, centerX: 32, centerY: 32, radius: 14, elevation: 0.6f, config.LandElevationThreshold);
        StampLandBlob(plan, centerX: 8, centerY: 8, radius: 5, elevation: 0.55f, config.LandElevationThreshold);
        StampLandBlob(plan, centerX: 55, centerY: 50, radius: 5, elevation: 0.55f, config.LandElevationThreshold);
        StampLandBlob(plan, centerX: 20, centerY: 58, radius: 1, elevation: 0.55f, config.LandElevationThreshold);

        LandConnectivityStage.Execute(plan, config);

        Assert.Equal(3, CountLandComponents(plan));
        Assert.Equal(0, CountOrphanLandCells(plan, config.MinLandComponentCells));
    }

    private static void StampLandBlob(
        IslandPlan plan,
        int centerX,
        int centerY,
        int radius,
        float elevation,
        float landThreshold)
    {
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.Elevation = elevation;
                cell.IsLand = elevation > landThreshold;
                cell.Biome = BiomeId.Plains;
            }
        }
    }

    private static int CountOrphanLandCells(IslandPlan plan, int minComponentCells)
    {
        var components = FindLandComponents(plan);
        int orphanCells = 0;

        foreach (List<(int X, int Y)> component in components)
        {
            if (component.Count < minComponentCells)
            {
                orphanCells += component.Count;
            }
        }

        return orphanCells;
    }

    private static int CountLandComponents(IslandPlan plan) => FindLandComponents(plan).Count;

    private static List<List<(int X, int Y)>> FindLandComponents(IslandPlan plan)
    {
        (int Dx, int Dy)[] neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        var visited = new bool[plan.Width * plan.Height];
        var components = new List<List<(int X, int Y)>>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int startIndex = y * plan.Width + x;
                if (visited[startIndex] || !plan.IsLand(x, y))
                {
                    continue;
                }

                var component = new List<(int X, int Y)>();
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y));
                visited[startIndex] = true;

                while (queue.Count > 0)
                {
                    (int cx, int cy) = queue.Dequeue();
                    component.Add((cx, cy));

                    foreach ((int dx, int dy) in neighbors)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (!plan.Contains(nx, ny))
                        {
                            continue;
                        }

                        int neighborIndex = ny * plan.Width + nx;
                        if (visited[neighborIndex] || !plan.IsLand(nx, ny))
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue.Enqueue((nx, ny));
                    }
                }

                components.Add(component);
            }
        }

        return components;
    }
}
