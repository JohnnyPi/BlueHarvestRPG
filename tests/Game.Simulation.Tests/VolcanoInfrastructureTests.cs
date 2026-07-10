using Game.Content.Definitions;
using Game.Generation.LocalMaps;
using Game.Generation.Passes;
using Game.Generation.Regional;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public sealed class VolcanoInfrastructureTests
{
    [Fact]
    public void GeneratedInfrastructure_StaysOutsideProtectedVolcanoCore()
    {
        IslandPlan plan = Generate(76001UL).IslandPlan!;
        FacilityRoadNode hub = Assert.Single(
            plan.RoadGraph.Nodes,
            node => node.Kind == FacilityRoadNodeKind.Hub);

        Assert.NotEmpty(plan.VolcanoExclusion.Zones);
        Assert.False(plan.VolcanoExclusion.IsProtected(hub.Cell.X, hub.Cell.Y));
        Assert.DoesNotContain(
            plan.RoadGraph.PathCells,
            cell => plan.VolcanoExclusion.IsProtected(cell.X, cell.Y));
        Assert.DoesNotContain(
            plan.RiverGraph.PathCells,
            cell => plan.IsLand(cell.X, cell.Y) && plan.VolcanoExclusion.IsProtected(cell.X, cell.Y));
        foreach (StructurePlacement structure in plan.Structures)
        {
            int minCellX = structure.GlobalOriginX / LocalMap.Width;
            int minCellY = structure.GlobalOriginY / LocalMap.Height;
            int maxCellX = (structure.GlobalOriginX + structure.Width - 1) / LocalMap.Width;
            int maxCellY = (structure.GlobalOriginY + structure.Height - 1) / LocalMap.Height;
            for (int y = minCellY; y <= maxCellY; y++)
            {
                for (int x = minCellX; x <= maxCellX; x++)
                {
                    Assert.False(plan.VolcanoExclusion.IsProtected(x, y));
                    Assert.DoesNotContain((x, y), plan.LavaFlowGraph.PathCells);
                }
            }
        }
    }

    [Fact]
    public void RoadNetwork_BuildsVolcanoRingAndUsesRoutedGlobalChains()
    {
        IslandPlan plan = Generate(4242UL).IslandPlan!;

        Assert.Contains(plan.RoadGraph.Nodes, node => node.Kind == FacilityRoadNodeKind.Ring);
        Assert.True(GlobalTileConnectivityValidator.IsConnected(plan.RoadGraph.GlobalPathTiles));
        foreach (FacilityRoadSegment segment in plan.RoadGraph.Segments)
        {
            Assert.All(
                segment.Path.Zip(segment.Path.Skip(1)),
                pair => Assert.Equal(
                    1,
                    Math.Abs(pair.First.X - pair.Second.X) + Math.Abs(pair.First.Y - pair.Second.Y)));
        }
    }

    [Fact]
    public void LavaFlows_AreDeterministicDownhillHazardsWithPresentation()
    {
        Overworld first = Generate(76003UL);
        Overworld second = Generate(76003UL);
        IslandPlan firstPlan = first.IslandPlan!;
        IslandPlan secondPlan = second.IslandPlan!;

        Assert.NotEmpty(firstPlan.LavaFlowGraph.Flows);
        Assert.Equal(firstPlan.LavaFlowGraph.GlobalLavaTiles, secondPlan.LavaFlowGraph.GlobalLavaTiles);

        foreach (LavaFlow flow in firstPlan.LavaFlowGraph.Flows)
        {
            Assert.True(
                firstPlan.GetCell(flow.Path[^1]).Elevation <= firstPlan.GetCell(flow.Path[0]).Elevation,
                "A lava flow endpoint should not be above its volcanic source.");
        }

        (int globalX, int globalY) = firstPlan.LavaFlowGraph.GlobalLavaTiles.First();
        (WorldCoord cell, LocalCoord local) =
            CoordinateMath.FromGlobalTile(new GlobalTileCoord(globalX, globalY));
        var map = new LocalMap(MapKey.Surface(cell));
        map.SetTerrain(local.X, local.Y, TerrainId.Dirt, TileFlags.None);
        var context = new LocalGenerationContext
        {
            Seed = first.Seed,
            WorldCoordinate = cell,
            WorldCell = first.GetCellValue(cell),
            Connections = [],
            IslandPlan = firstPlan,
            BlueprintCatalog = StructureBlueprintCatalogDefaults.Create()
        };
        new LavaFlowStampPass().Execute(map, context);

        Assert.Equal(TerrainId.Lava, map.Terrain[map.GetIndex(local.X, local.Y)]);
        Assert.True(map.BlocksMovement(local));
    }

    private static Overworld Generate(ulong seed)
        => new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
}
