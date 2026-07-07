using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class EdgeConnectionTests
{
    [Fact]
    public void RegionalFeatureGraph_ProducesIdenticalConnectionsForSameSeed()
    {
        const ulong seed = 13579UL;
        var generator = new OverworldGenerator();

        Overworld first = generator.Generate(64, 64, seed);
        Overworld second = generator.Generate(64, 64, seed);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                var coord = new WorldCoord(x, y);
                ReadOnlySpan<EdgeConnection> firstConnections = first.GetEdgeConnections(coord);
                ReadOnlySpan<EdgeConnection> secondConnections = second.GetEdgeConnections(coord);

                Assert.Equal(firstConnections.Length, secondConnections.Length);
                for (int i = 0; i < firstConnections.Length; i++)
                {
                    Assert.Equal(firstConnections[i], secondConnections[i]);
                }
            }
        }
    }

    [Fact]
    public void AdjacentCells_HaveMirroredEastWestRoadConnections()
    {
        Overworld world = new OverworldGenerator().Generate(64, 64, 24680UL);

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var westCoord = new WorldCoord(x, y);
                var eastCoord = new WorldCoord(x + 1, y);

                foreach (EdgeConnection westConnection in world.GetEdgeConnections(westCoord))
                {
                    if (westConnection.Type != ConnectionType.Road || westConnection.Edge != Direction.East)
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
    public void AdjacentCells_HaveMirroredNorthSouthRoadConnections()
    {
        Overworld world = new OverworldGenerator().Generate(64, 64, 24680UL);

        for (int y = 0; y < world.Height - 1; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                var northCoord = new WorldCoord(x, y);
                var southCoord = new WorldCoord(x, y + 1);

                foreach (EdgeConnection northConnection in world.GetEdgeConnections(northCoord))
                {
                    if (northConnection.Type != ConnectionType.Road || northConnection.Edge != Direction.South)
                    {
                        continue;
                    }

                    EdgeConnection? mirrored = FindMatchingConnection(
                        world.GetEdgeConnections(southCoord),
                        Direction.North,
                        northConnection.LocalOffset,
                        northConnection.Width);

                    Assert.True(mirrored.HasValue);
                    Assert.True(northConnection.Mirrors(mirrored.Value));
                }
            }
        }
    }

    [Fact]
    public void LocalMapGenerator_StampAlignedRoadTilesAtSharedBoundary()
    {
        const ulong seed = 24680UL;
        Overworld world = new OverworldGenerator().Generate(64, 64, seed);
        var generator = new LocalMapGenerator();

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var westCoord = new WorldCoord(x, y);
                var eastCoord = new WorldCoord(x + 1, y);

                foreach (EdgeConnection connection in world.GetEdgeConnections(westCoord))
                {
                    if (connection.Type != ConnectionType.Road || connection.Edge != Direction.East)
                    {
                        continue;
                    }

                    LocalMap westMap = generator.Generate(world, westCoord);
                    LocalMap eastMap = generator.Generate(world, eastCoord);

                    for (int i = 0; i < connection.Width; i++)
                    {
                        int offset = connection.LocalOffset + i;
                        TerrainId westEdge = westMap.Terrain[westMap.GetIndex(LocalMap.Width - 1, offset)];
                        TerrainId eastEdge = eastMap.Terrain[eastMap.GetIndex(0, offset)];

                        Assert.Equal(TerrainId.Road, westEdge);
                        Assert.Equal(TerrainId.Road, eastEdge);
                    }
                }
            }
        }
    }

    [Fact]
    public void TryMoveLocal_CanCrossSharedRoadBoundaryWithoutManualTerrainPrep()
    {
        const ulong seed = 24680UL;
        Overworld world = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, new LocalMapGenerator());
        var session = new Game.Simulation.Session.GameSession(world, repository);

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var westCoord = new WorldCoord(x, y);
                var eastCoord = new WorldCoord(x + 1, y);

                foreach (EdgeConnection connection in world.GetEdgeConnections(westCoord))
                {
                    if (connection.Type != ConnectionType.Road || connection.Edge != Direction.East)
                    {
                        continue;
                    }

                    session.PlayerWorldPosition = westCoord;
                    session.EnterWorldCell(new LocalCoord(LocalMap.Width - 1, connection.LocalOffset));

                    Assert.True(session.TryMoveLocal(1, 0));
                    Assert.Equal(eastCoord, session.PlayerWorldPosition);
                    Assert.Equal(new LocalCoord(0, connection.LocalOffset), session.PlayerLocalPosition);
                    Assert.Equal(TerrainId.Road, session.ActiveLocalMap!.Terrain[
                        session.ActiveLocalMap.GetIndex(session.PlayerLocalPosition.X, session.PlayerLocalPosition.Y)]);

                    session.PlayerWorldPosition = westCoord;
                    session.EnterWorldCell(new LocalCoord(LocalMap.Width - 1, connection.LocalOffset));
                    Assert.Equal(westCoord, session.PlayerWorldPosition);
                    return;
                }
            }
        }

        Assert.Fail("Expected at least one east road connection in generated world.");
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
                connection.Type == ConnectionType.Road &&
                connection.LocalOffset == localOffset &&
                connection.Width == width)
            {
                return connection;
            }
        }

        return null;
    }
}
