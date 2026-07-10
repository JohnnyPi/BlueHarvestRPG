using Game.Generation.LocalMaps;
using Game.Generation.Regional;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class FacilityRoadNetworkTests
{
    private static readonly ulong[] Seeds = [1234UL, 4242UL, 777UL, 999UL];

    [Fact]
    public void RoadGraph_IsDeterministicForSameSeed()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.Island);
        Overworld first = generator.Generate(4242UL);
        Overworld second = generator.Generate(4242UL);

        Assert.Equal(first.IslandPlan!.RoadGraph.PathCells, second.IslandPlan!.RoadGraph.PathCells);
        Assert.Equal(
            first.IslandPlan.RoadGraph.GlobalPathTiles,
            second.IslandPlan.RoadGraph.GlobalPathTiles);
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void RoadGraph_ConnectsHubToFacilities(ulong seed)
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
        IslandPlan plan = world.IslandPlan!;

        Assert.NotEmpty(plan.RoadGraph.PathCells);
        Assert.Contains(plan.RoadGraph.Nodes, node => node.Kind == FacilityRoadNodeKind.Hub);

        foreach (StructurePlacement structure in plan.Structures)
        {
            WorldCoord structureCell = StructureCell(plan, structure);
            Assert.True(
                plan.RoadGraph.IsAdjacentToRoad(structureCell) ||
                plan.RoadGraph.PathCells.Contains((structureCell.X, structureCell.Y)),
                $"Structure {structure.Type} at {structureCell} is not connected to roads.");
        }
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void RoadCells_HaveRoadRole(ulong seed)
    {
        IslandPlan plan = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed).IslandPlan!;

        foreach ((int x, int y) in plan.RoadGraph.PathCells)
        {
            Assert.True(
                plan.GetCell(x, y).Role.HasFlag(IslandCellRole.Road),
                $"Road path cell ({x},{y}) missing Road role.");
        }
    }

    [Theory]
    [MemberData(nameof(SeedData))]
    public void RoadCells_ContainLocalRoadTerrain(ulong seed)
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
        var localGenerator = new LocalMapGenerator();
        IslandPlan plan = world.IslandPlan!;

        Assert.NotEmpty(plan.RoadGraph.GlobalPathTiles);
        foreach ((int globalX, int globalY) in plan.RoadGraph.GlobalPathTiles)
        {
            (WorldCoord cell, LocalCoord local) = CoordinateMath.FromGlobalTile(new GlobalTileCoord(globalX, globalY));
            LocalMap map = localGenerator.Generate(world, MapKey.Surface(cell));
            TerrainId terrain = map.Terrain[map.GetIndex(local.X, local.Y)];
            Assert.True(
                terrain is TerrainId.Road or TerrainId.ShallowFord or TerrainId.Door,
                $"Global road tile ({globalX},{globalY}) is {terrain}, expected Road.");
        }
    }

    [Fact]
    public void PlannedRoads_CreateEdgeConnections()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        bool hasRoadConnection = false;

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                ReadOnlySpan<EdgeConnection> connections = world.GetEdgeConnections(coord);
                for (int i = 0; i < connections.Length; i++)
                {
                    if (connections[i].Type == ConnectionType.Road)
                    {
                        hasRoadConnection = true;
                        break;
                    }
                }
            }

            if (hasRoadConnection)
            {
                break;
            }
        }

        Assert.True(hasRoadConnection);
    }

    [Fact]
    public void GlobalRoadTiles_AreOneConnectedComponent()
    {
        IslandPlan plan = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL).IslandPlan!;

        Assert.NotEmpty(plan.RoadGraph.GlobalPathTiles);
        Assert.True(GlobalTileConnectivityValidator.IsConnected(plan.RoadGraph.GlobalPathTiles));
    }

    [Fact]
    public void StructureSpurs_EndAtResolvedBlueprintDoors()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        IslandPlan plan = world.IslandPlan!;
        var catalog = StructureBlueprintCatalogDefaults.Create();
        var localGenerator = new LocalMapGenerator(catalog);

        foreach (StructurePlacement structure in plan.Structures.Where(s => s.Type != StructureType.Helipad))
        {
            StructureBlueprintDefinition blueprint = catalog.ResolveById(structure.BlueprintId);
            (int doorX, int doorY) = StructurePlacementQueries.DoorGlobal(structure, blueprint);
            Assert.Contains((doorX, doorY), plan.RoadGraph.GlobalPathTiles);
            Assert.Contains(
                new[] { (doorX + 1, doorY), (doorX - 1, doorY), (doorX, doorY + 1), (doorX, doorY - 1) },
                tile => plan.RoadGraph.GlobalPathTiles.Contains(tile));

            (WorldCoord cell, LocalCoord local) =
                CoordinateMath.FromGlobalTile(new GlobalTileCoord(doorX, doorY));
            LocalMap map = localGenerator.Generate(world, MapKey.Surface(cell));
            Assert.Equal(TerrainId.Door, map.Terrain[map.GetIndex(local.X, local.Y)]);
        }
    }

    public static IEnumerable<object[]> SeedData => Seeds.Select(seed => new object[] { seed });

    private static WorldCoord StructureCell(IslandPlan plan, StructurePlacement structure)
    {
        int centerGx = structure.GlobalOriginX + structure.Width / 2;
        int centerGy = structure.GlobalOriginY + structure.Height / 2;
        return new WorldCoord(centerGx / LocalMap.Width, centerGy / LocalMap.Height);
    }
}
