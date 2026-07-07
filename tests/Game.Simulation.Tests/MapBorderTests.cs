using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class MapBorderTests
{
    [Theory]
    [InlineData(0, 24, Direction.West)]
    [InlineData(63, 24, Direction.East)]
    [InlineData(24, 0, Direction.North)]
    [InlineData(24, 63, Direction.South)]
    public void IsBorderTile_DetectsEdgeTiles(int x, int y, Direction expectedEdge)
    {
        Assert.True(MapBorderHelper.IsBorderTile(x, y));
        Assert.True(MapBorderHelper.IsOnEdge(expectedEdge, x, y));
    }

    [Fact]
    public void QueueMoveToBorderTransition_WalksToBorderBeforeTransitioning()
    {
        const ulong seed = 24680UL;
        Overworld world = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, new LocalMapGenerator());
        var session = new GameSession(world, repository)
        {
            PlayerWorldPosition = new WorldCoord(10, 8)
        };

        session.EnterWorldCell(new LocalCoord(30, 24));
        LocalMap map = session.ActiveLocalMap!;
        for (int x = 30; x <= 63; x++)
        {
            map.SetTerrain(x, 24, TerrainId.Road, TileFlags.None);
        }

        PrepareWalkableTransition(world, repository, new WorldCoord(10, 8), new LocalCoord(63, 24), 1, 0);

        Assert.True(session.QueueMoveToBorderTransition(63, 24, Direction.East));
        Assert.Equal(new LocalCoord(30, 24), session.PlayerLocalPosition);

        while (session.HasQueuedMovement)
        {
            session.AdvanceMovement();
        }

        Assert.Equal(new WorldCoord(11, 8), session.PlayerWorldPosition);
        Assert.Equal(new LocalCoord(0, 24), session.PlayerLocalPosition);
    }

    [Fact]
    public void TryTransitionAcrossEdge_MovesPlayerToNeighborFromBorderMenu()
    {
        const ulong seed = 24680UL;
        Overworld world = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, new LocalMapGenerator());
        var session = new GameSession(world, repository)
        {
            PlayerWorldPosition = new WorldCoord(10, 8)
        };

        session.EnterWorldCell(new LocalCoord(63, 24));
        PrepareWalkableTransition(world, repository, new WorldCoord(10, 8), new LocalCoord(63, 24), 1, 0);

        Assert.True(session.TryTransitionAcrossEdge(Direction.East, 63, 24));
        Assert.Equal(new WorldCoord(11, 8), session.PlayerWorldPosition);
        Assert.Equal(new LocalCoord(0, 24), session.PlayerLocalPosition);
    }

    [Fact]
    public void CanTransitionAcrossEdge_ReturnsFalseOutsideWorldBounds()
    {
        Overworld world = new OverworldGenerator().Generate(64, 64, 42UL);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, new LocalMapGenerator());
        var session = new GameSession(world, repository)
        {
            PlayerWorldPosition = new WorldCoord(0, 0)
        };

        session.EnterWorldCell(new LocalCoord(0, 24));

        Assert.False(session.CanTransitionAcrossEdge(Direction.West, 0, 24));
        Assert.False(session.CanTransitionAcrossEdge(Direction.North, 24, 0));
    }

    private static void PrepareWalkableTransition(
        Overworld overworld,
        ILocalMapRepository repository,
        WorldCoord worldPosition,
        LocalCoord localPosition,
        int deltaX,
        int deltaY)
    {
        LocalMap origin = repository.GetOrGenerateSurface(worldPosition);
        origin.SetTerrain(localPosition.X, localPosition.Y, TerrainId.Grass, TileFlags.None);

        if (!MapTransitionResolver.TryResolve(
                overworld,
                worldPosition,
                localPosition,
                deltaX,
                deltaY,
                out MapTransition transition))
        {
            return;
        }

        LocalMap destination = repository.GetOrGenerateSurface(transition.DestinationWorld);
        destination.SetTerrain(
            transition.DestinationLocal.X,
            transition.DestinationLocal.Y,
            TerrainId.Grass,
            TileFlags.None);
    }
}
