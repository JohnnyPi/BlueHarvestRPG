using System.Diagnostics;
using Game.Generation.Island;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandGeneratorTests
{
    [Fact]
    public void IslandWorldGenerator_ProducesIdenticalPlansForSameSeed()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.Island);
        const ulong seed = 12345UL;

        Overworld first = generator.Generate(seed);
        Overworld second = generator.Generate(seed);

        Assert.NotNull(first.IslandPlan);
        Assert.NotNull(second.IslandPlan);
        Assert.Equal(first.IslandPlan!.Structures.Count, second.IslandPlan!.Structures.Count);
        Assert.Equal(first.IslandPlan.FenceRings.Count, second.IslandPlan.FenceRings.Count);
        Assert.Equal(first.IslandPlan.RuinSites.Count, second.IslandPlan.RuinSites.Count);

        for (int y = 0; y < TestSaveDefaults.Island.OverworldSize; y++)
        {
            for (int x = 0; x < TestSaveDefaults.Island.OverworldSize; x++)
            {
                var coord = new WorldCoord(x, y);
                Assert.Equal(first.GetCellValue(coord).Biome, second.GetCellValue(coord).Biome);
                Assert.Equal(first.IslandPlan.GetCell(coord).IsLand, second.IslandPlan.GetCell(coord).IsLand);
            }
        }
    }

    [Fact]
    public void IslandPlan_ContainsRequiredParkFeatures()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(999UL);
        IslandPlan plan = world.IslandPlan!;

        Assert.Contains(plan.Structures, s => s.Type == StructureType.Dock);
        Assert.Contains(plan.Structures, s => s.Type == StructureType.VisitorCenter);
        Assert.Contains(plan.Structures, s => s.Type == StructureType.Hotel);
        Assert.Contains(plan.Structures, s => s.Type == StructureType.Restaurant);
        Assert.Contains(plan.Structures, s => s.Type == StructureType.Attraction);
        Assert.Contains(plan.Structures, s => s.Type == StructureType.MaintenanceCompound);
        Assert.NotEmpty(plan.FenceRings);
        Assert.NotEmpty(plan.TunnelGraph.AllTunnelTiles);
        Assert.Contains(plan.RuinSites, r => r.Kind == RuinKind.AncientRuin);
        Assert.Contains(plan.RuinSites, r => r.Kind == RuinKind.WarFortification);
    }

    [Fact]
    public void Island_HasLandOceanAndSatelliteIslands()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.Island).Generate(
            TestSaveDefaults.Island.OverworldSize,
            TestSaveDefaults.Island.OverworldSize,
            4242UL);

        int landCells = plan.Cells.Count(c => c.IsLand);
        int oceanCells = plan.Cells.Count(c => !c.IsLand);
        int coastCells = plan.Cells.Count(c => c.IsCoast);

        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);
        bool hasSatelliteLand = false;
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                float dx = (x - centerX) / maxRadius;
                float dy = (y - centerY) / maxRadius;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > TestSaveDefaults.Island.MainIslandRadius * 0.98f)
                {
                    hasSatelliteLand = true;
                    break;
                }
            }

            if (hasSatelliteLand)
            {
                break;
            }
        }

        Assert.True(landCells > 0);
        Assert.True(oceanCells > 0);
        Assert.True(coastCells > 0);
        Assert.True(hasSatelliteLand || plan.Regions.Any(r => r.IsSatelliteIsland));
    }

    [Fact]
    public void LocalMapGenerator_StampsStructuresDeterministically()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.Island);
        const ulong seed = 98765UL;
        Overworld world = generator.Generate(seed);
        var localGenerator = new LocalMapGenerator();

        WorldCoord coord = world.IslandPlan!.VisitorCenterCell;
        LocalMap first = localGenerator.Generate(world, MapKey.Surface(coord));
        LocalMap second = localGenerator.Generate(world, MapKey.Surface(coord));

        Assert.Equal(first.Terrain, second.Terrain);
        Assert.Equal(first.Flags, second.Flags);
        Assert.Contains(first.Terrain, t => t is TerrainId.Floor or TerrainId.Wall or TerrainId.Concrete or TerrainId.InteriorWall or TerrainId.Counter);
    }

    [Fact]
    public void FullIslandPlanner_CompletesWithinReasonableTime()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        var stopwatch = Stopwatch.StartNew();
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(
            TestSaveDefaults.FullIsland.OverworldSize,
            TestSaveDefaults.FullIsland.OverworldSize,
            1UL);
        stopwatch.Stop();

        Assert.Equal(TestSaveDefaults.FullIsland.OverworldSize, plan.Width);
        Assert.Equal(TestSaveDefaults.FullIsland.OverworldSize, plan.Height);
        Assert.NotEmpty(plan.Structures);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Planner took {stopwatch.Elapsed.TotalSeconds:F1}s");
    }
}
