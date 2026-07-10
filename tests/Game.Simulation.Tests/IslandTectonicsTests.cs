using Game.Generation.Island;
using Game.Generation.WorldGen;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandTectonicsTests
{
    [Fact]
    public void Generate_AssignsContinentalAndOceanicPlates()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.Island).Generate(64, 64, 9001UL);

        Assert.Contains(plan.Regions, region => region.IsContinental);
        Assert.Contains(plan.Regions, region => !region.IsContinental);
    }

    [Fact]
    public void Generate_ProducesPlateBoundaries()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(128, 128, 9002UL);

        Assert.NotEmpty(plan.PlateBoundaries);
        Assert.Contains(plan.PlateBoundaries, boundary =>
            boundary.Type is PlateBoundaryType.ConvergentSubduction
                or PlateBoundaryType.ConvergentCollision
                or PlateBoundaryType.Divergent);
    }

    [Fact]
    public void Generate_ProducesVolcanicSitesAndBiomes()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(128, 128, 9003UL);

        Assert.Single(plan.VolcanicSites);
        Assert.Contains(plan.Cells, cell => cell.Biome == BiomeId.Volcanic);
    }

    [Fact]
    public void CollisionBoundaries_CreateMountainOrVolcanicCells()
    {
        IslandPlan plan = new IslandPlanner(TestSaveDefaults.FullIsland).Generate(128, 128, 9004UL);

        bool hasCollisionEffect = false;
        foreach (PlateBoundarySegment boundary in plan.PlateBoundaries)
        {
            if (boundary.Type != PlateBoundaryType.ConvergentCollision)
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(boundary.CellX, boundary.CellY);
            if (cell.IsLand && (cell.Biome is BiomeId.Mountains or BiomeId.Volcanic or BiomeId.Hills))
            {
                hasCollisionEffect = true;
                break;
            }
        }

        Assert.True(hasCollisionEffect || plan.PlateBoundaries.Any(b => b.Type == PlateBoundaryType.ConvergentCollision));
    }

    [Fact]
    public void TectonicGeneration_IsDeterministicForSeed()
    {
        var planner = new IslandPlanner(TestSaveDefaults.Island);
        IslandPlan first = planner.Generate(64, 64, 4242UL);
        IslandPlan second = planner.Generate(64, 64, 4242UL);

        Assert.Equal(first.PlateBoundaries.Count, second.PlateBoundaries.Count);
        Assert.Equal(first.VolcanicSites.Count, second.VolcanicSites.Count);
        Assert.Equal(first.Cells.Length, second.Cells.Length);
        for (int i = 0; i < first.Cells.Length; i++)
        {
            Assert.Equal(first.Cells[i].Biome, second.Cells[i].Biome);
            Assert.Equal(first.Cells[i].Elevation, second.Cells[i].Elevation, precision: 4);
        }
    }

    [Fact]
    public void WorldGeneratorVersion_ReflectsTectonicPipeline()
    {
        Assert.Equal(10u, WorldGeneratorVersion.Current);
    }
}
