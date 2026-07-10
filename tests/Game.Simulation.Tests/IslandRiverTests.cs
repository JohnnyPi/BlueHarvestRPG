using Game.Generation.LocalMaps;
using Game.Generation.Regional;
using Game.Generation.WorldGen;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class IslandRiverTests
{
    [Fact]
    public void Generate_ProducesRiverConnections()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(128, 128, 8801UL);

        int riverConnections = CountConnections(world, ConnectionType.River);
        Assert.True(riverConnections > 0);
    }

    [Fact]
    public void RiverGeneration_IsDeterministicForSeed()
    {
        var generator = new IslandWorldGenerator(TestSaveDefaults.FullIsland);
        Overworld first = generator.Generate(128, 128, 8802UL);
        Overworld second = generator.Generate(128, 128, 8802UL);

        Assert.Equal(
            CountConnections(first, ConnectionType.River),
            CountConnections(second, ConnectionType.River));
    }

    [Fact]
    public void AdjacentCells_HaveMirroredRiverConnections()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(128, 128, 8803UL);

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var westCoord = new WorldCoord(x, y);
                var eastCoord = new WorldCoord(x + 1, y);

                foreach (EdgeConnection westConnection in world.GetEdgeConnections(westCoord))
                {
                    if (westConnection.Type != ConnectionType.River || westConnection.Edge != Direction.East)
                    {
                        continue;
                    }

                    EdgeConnection? mirrored = FindMatchingConnection(
                        world.GetEdgeConnections(eastCoord),
                        Direction.West,
                        westConnection.LocalOffset,
                        westConnection.Width);

                    Assert.True(mirrored.HasValue);
                    Assert.True(westConnection.Mirrors(mirrored.Value));
                }
            }
        }
    }

    [Fact]
    public void LocalMapGenerator_StampAlignedRiverTilesAtSharedBoundary()
    {
        const ulong seed = 8804UL;
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(128, 128, seed);
        var generator = new LocalMapGenerator();

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var westCoord = new WorldCoord(x, y);
                var eastCoord = new WorldCoord(x + 1, y);

                foreach (EdgeConnection connection in world.GetEdgeConnections(westCoord))
                {
                    if (connection.Type != ConnectionType.River || connection.Edge != Direction.East)
                    {
                        continue;
                    }

                    LocalMap westMap = generator.Generate(world, MapKey.Surface(westCoord));
                    LocalMap eastMap = generator.Generate(world, MapKey.Surface(eastCoord));

                    for (int i = 0; i < connection.Width; i++)
                    {
                        int offset = connection.LocalOffset + i;
                        TerrainId westEdge = westMap.Terrain[westMap.GetIndex(LocalMap.Width - 1, offset)];
                        TerrainId eastEdge = eastMap.Terrain[eastMap.GetIndex(0, offset)];

                        Assert.Equal(TerrainId.ShallowFord, westEdge);
                        Assert.Equal(TerrainId.ShallowFord, eastEdge);
                    }

                    return;
                }
            }
        }

        Assert.Fail("Expected at least one east river connection in generated world.");
    }

    [Fact]
    public void RiverTraces_AreContiguousAndTerminateInExteriorOcean()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(128, 128, 8804UL);
        IslandPlan plan = world.IslandPlan!;
        var localGenerator = new LocalMapGenerator();

        Assert.NotEmpty(plan.RiverGraph.Segments);
        foreach (FacilityRiverSegment segment in plan.RiverGraph.Segments)
        {
            var segmentTiles = new HashSet<(int GlobalX, int GlobalY)>();
            GlobalTilePathUtility.AddPathWithBorderRuns(
                segmentTiles,
                segment.Path,
                TestSaveDefaults.FullIsland.RiverWidth);
            Assert.True(GlobalTileConnectivityValidator.IsConnected(segmentTiles));

            WorldCoord mouth = segment.Path[^1];
            Assert.False(plan.IsLand(mouth.X, mouth.Y));
            Assert.True(IsExteriorWater(plan, mouth));

            (int mouthX, int mouthY) = FacilityRoadGraph.CellCenterGlobal(mouth);
            Assert.Contains((mouthX, mouthY), plan.RiverGraph.GlobalRiverTiles);
            LocalMap mouthMap = localGenerator.Generate(world, MapKey.Surface(mouth));
            Assert.Equal(
                TerrainId.ShallowFord,
                mouthMap.Terrain[mouthMap.GetIndex(LocalMap.Width / 2, LocalMap.Height / 2)]);
        }
    }

    [Fact]
    public void BuildRenderSnapshot_IncludesGeologyOverlays()
    {
        SimulationHost host = CreateHost();
        WorldCoord? riverCell = FindRiverCell(host.Overworld);
        Assert.NotNull(riverCell);
        OverworldExploration.RevealAround(host.Overworld, riverCell.Value, 0);

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.NotNull(snapshot.TectonicBoundaries);
        Assert.NotNull(snapshot.RiverEdgeMask);
        Assert.Contains(snapshot.TectonicBoundaries, boundary => boundary != 0);
        Assert.Contains(snapshot.RiverEdgeMask, mask => mask != 0);
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(128, 128, 8805UL);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new Game.Simulation.Session.GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }

    private static WorldCoord? FindRiverCell(Overworld world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                foreach (EdgeConnection connection in world.GetEdgeConnections(coord))
                {
                    if (connection.Type == ConnectionType.River)
                    {
                        return coord;
                    }
                }
            }
        }

        return null;
    }

    private static int CountConnections(Overworld world, ConnectionType type)
    {
        int count = 0;
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                foreach (EdgeConnection connection in world.GetEdgeConnections(new WorldCoord(x, y)))
                {
                    if (connection.Type == type)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static EdgeConnection? FindMatchingConnection(
        ReadOnlySpan<EdgeConnection> connections,
        Direction edge,
        int localOffset,
        int width)
    {
        foreach (EdgeConnection connection in connections)
        {
            if (connection.Edge == edge &&
                connection.Type == ConnectionType.River &&
                connection.LocalOffset == localOffset &&
                connection.Width == width)
            {
                return connection;
            }
        }

        return null;
    }

    private static bool IsExteriorWater(IslandPlan plan, WorldCoord start)
    {
        var visited = new HashSet<(int X, int Y)> { (start.X, start.Y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((start.X, start.Y));

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            if (x == 0 || y == 0 || x == plan.Width - 1 || y == plan.Height - 1)
            {
                return true;
            }

            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx;
                int ny = y + dy;
                if (plan.Contains(nx, ny) && !plan.IsLand(nx, ny) && visited.Add((nx, ny)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return false;
    }
}
