using Game.Content.Definitions;
using Game.Generation.Biomes;
using Game.Generation.Island;
using Game.Generation.Island.Stages;
using Game.Generation.LocalMaps;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public sealed class WorldGenerationEnhancementTests
{
    [Fact]
    public void BiomeDepth_GradesFromBiomeEdgeToInterior()
    {
        var plan = new IslandPlan(27, 27, 1UL);
        foreach (ref IslandCellData cell in plan.Cells.AsSpan())
        {
            cell.IsLand = true;
            cell.Biome = BiomeId.Jungle;
        }

        BiomeDepthStage.Execute(plan, new IslandDefinition());

        Assert.Equal(0f, plan.BiomeDepth[13]);
        Assert.Equal(1f, plan.BiomeDepth[13 * plan.Width + 13]);
        Assert.True(plan.BiomeDepth[13 * plan.Width + 6] < plan.BiomeDepth[13 * plan.Width + 12]);
    }

    [Fact]
    public void Planner_ComputesBiomeDepthFromFinalBiomes()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.Island).Generate(64, 64, 1441UL);
        (int Dx, int Dy)[] neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                BiomeId biome = plan.GetCell(x, y).Biome;
                bool edge = neighbors.Any(offset =>
                {
                    int nx = x + offset.Dx;
                    int ny = y + offset.Dy;
                    return !plan.Contains(nx, ny)
                        || !plan.IsLand(nx, ny)
                        || plan.GetCell(nx, ny).Biome != biome;
                });

                if (edge)
                {
                    Assert.Equal(0f, plan.BiomeDepth[y * plan.Width + x]);
                }
            }
        }
    }

    [Fact]
    public void LocalTerrainField_IsDeterministicAndContinuousAcrossCellBoundary()
    {
        var first = new LocalTerrainField(9876UL);
        var second = new LocalTerrainField(9876UL);
        (int leftX, int leftY) = LocalTerrainField.ToGlobalTile(0, 0, LocalMap.Width, 17);
        (int rightX, int rightY) = LocalTerrainField.ToGlobalTile(1, 0, 0, 17);

        Assert.Equal((leftX, leftY), (rightX, rightY));
        Assert.Equal(first.SampleDensity(leftX, leftY), second.SampleDensity(rightX, rightY));
        Assert.Equal(first.SampleAccent(leftX, leftY), second.SampleAccent(rightX, rightY));

        float boundaryValue = first.SampleDensity(rightX, rightY);
        float adjacentValue = first.SampleDensity(rightX - 1, rightY);
        Assert.InRange(MathF.Abs(boundaryValue - adjacentValue), 0f, 0.25f);
    }

    [Fact]
    public void JungleVegetationDensity_IncreasesWithBiomeDepth()
    {
        LocalMap edge = GenerateJungleMap(0f);
        LocalMap interior = GenerateJungleMap(1f);

        int edgeVegetation = edge.Terrain.Count(IsJungleVegetation);
        int interiorVegetation = interior.Terrain.Count(IsJungleVegetation);

        Assert.True(interiorVegetation > edgeVegetation);
        Assert.Contains(TerrainId.DenseCanopy, interior.Terrain);
        Assert.Contains(TerrainId.Undergrowth, edge.Terrain);
    }

    [Fact]
    public void HeightBandResolver_UsesAllConfiguredBands()
    {
        var rules = new BiomeRulesDefinition
        {
            FoothillsMinElevation = 0.50f,
            HillsMinElevation = 0.60f,
            SmallMountainMinElevation = 0.70f,
            MountainsMinElevation = 0.80f
        };

        Assert.Equal(HighlandBand.Foothills, HeightBandResolver.Resolve(0.50f, rules));
        Assert.Equal(HighlandBand.Hills, HeightBandResolver.Resolve(0.60f, rules));
        Assert.Equal(HighlandBand.SmallMountains, HeightBandResolver.Resolve(0.70f, rules));
        Assert.Equal(HighlandBand.Mountains, HeightBandResolver.Resolve(0.80f, rules));
    }

    [Fact]
    public void Volcano_IsSingleCircularAndHasMonotonicProfile()
    {
        var plan = new IslandPlan(81, 81, 42UL);
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                cell.IsLand = true;
                cell.Elevation = 0.42f + ((x * 17 + y * 31) % 9) * 0.01f;
            }
        }

        var config = new IslandDefinition
        {
            MinOceanBorderCells = 4,
            VolcanicConeCount = 3,
            VolcanicConeRadius = 0.25f,
            VolcanicConeHeight = 0.55f
        };

        VolcanicActivityStage.Execute(plan, config, 123UL);

        VolcanicSite site = Assert.Single(plan.VolcanicSites);
        Assert.Equal(site.RadiusX, site.RadiusY);
        Assert.Equal(0f, site.RotationRadians);

        int maxOffset = (int)MathF.Floor(site.RadiusX * VolcanicConeUtility.ApronExtent);
        float previous = float.MaxValue;
        for (int offset = 0; offset <= maxOffset; offset++)
        {
            float east = plan.GetCell(site.X + offset, site.Y).Elevation;
            float west = plan.GetCell(site.X - offset, site.Y).Elevation;
            Assert.Equal(east, west, precision: 5);
            Assert.True(east <= previous + 0.00001f);
            previous = east;
        }

        float profilePrevious = float.MaxValue;
        for (int step = 0; step <= 25; step++)
        {
            float elevation = VolcanicConeUtility.EvaluateElevationProfile(
                0.4f,
                0.5f,
                step / 20f);
            Assert.True(elevation <= profilePrevious + 0.00001f);
            profilePrevious = elevation;
        }

        Assert.Equal(
            0.4f,
            VolcanicConeUtility.EvaluateElevationProfile(
                0.4f,
                0.5f,
                VolcanicConeUtility.ApronExtent),
            precision: 5);
    }

    private static LocalMap GenerateJungleMap(float biomeDepth)
    {
        var world = new Overworld(1, 1, 555UL);
        var plan = new IslandPlan(1, 1, world.Seed)
        {
            BiomeDepth = [biomeDepth]
        };
        ref IslandCellData islandCell = ref plan.GetCell(0, 0);
        islandCell.IsLand = true;
        islandCell.Biome = BiomeId.Jungle;
        islandCell.Elevation = 0.55f;
        islandCell.Moisture = 0.85f;
        world.IslandPlan = plan;

        ref WorldCell worldCell = ref world.GetCell(new WorldCoord(0, 0));
        worldCell.Biome = BiomeId.Jungle;
        worldCell.Elevation = islandCell.Elevation;
        worldCell.Moisture = islandCell.Moisture;

        return new LocalMapGenerator(
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules).GenerateSurface(world, new WorldCoord(0, 0));
    }

    private static bool IsJungleVegetation(TerrainId terrain)
        => terrain is TerrainId.Tree or TerrainId.Undergrowth or TerrainId.DenseCanopy;
}
