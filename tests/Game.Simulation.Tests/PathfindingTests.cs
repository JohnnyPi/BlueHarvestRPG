using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Input;
using Game.Simulation.LocalMaps;
using Game.Simulation.Pathfinding;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class PathfindingTests
{
    [Fact]
    public void FindPath_ReturnsStraightLineOnOpenGrid()
    {
        List<(int X, int Y)> path = GridPathfinder.FindPath(0, 0, 3, 0, 8, 8, static (_, _) => false);

        Assert.Equal(3, path.Count);
        Assert.Equal((3, 0), path[^1]);
    }

    [Fact]
    public void FindPath_ReturnsEmptyForSameTile()
    {
        List<(int X, int Y)> path = GridPathfinder.FindPath(2, 2, 2, 2, 8, 8, static (_, _) => false);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_ReturnsEmptyWhenTargetBlocked()
    {
        List<(int X, int Y)> path = GridPathfinder.FindPath(0, 0, 3, 3, 8, 8, (x, y) => x == 3 && y == 3);
        Assert.Empty(path);
    }

    [Fact]
    public void FindPath_RoutesAroundWall()
    {
        bool Blocked(int x, int y) => x == 1 && y <= 1;

        List<(int X, int Y)> path = GridPathfinder.FindPath(0, 0, 2, 0, 8, 8, Blocked);

        Assert.NotEmpty(path);
        Assert.Equal((2, 0), path[^1]);
        Assert.DoesNotContain((1, 0), path);
    }

    [Fact]
    public void FindPath_ReturnsEmptyWhenUnreachable()
    {
        bool Blocked(int x, int y) => x == 1;

        List<(int X, int Y)> path = GridPathfinder.FindPath(0, 0, 2, 0, 3, 3, Blocked);
        Assert.Empty(path);
    }
}

public class MovementQueueTests
{
    [Fact]
    public void QueueMoveTo_WalksOneTilePerTickOnOverworld()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 650;

        host.QueueIntent(GameIntent.MoveToSelected, 13, 10);
        host.Tick();
        Assert.Equal(new WorldCoord(11, 10), host.Session.PlayerWorldPosition);

        host.Tick();
        Assert.Equal(new WorldCoord(12, 10), host.Session.PlayerWorldPosition);

        host.Tick();
        Assert.Equal(new WorldCoord(13, 10), host.Session.PlayerWorldPosition);
        Assert.False(host.Session.HasQueuedMovement);
    }

    [Fact]
    public void QueueMoveTo_WalksOneTilePerTickOnLocalMap()
    {
        SimulationHost host = CreateHost(7UL);
        host.Session.EnterWorldCell();
        LocalMap map = host.Session.ActiveLocalMap!;

        for (int x = 0; x < LocalMap.Width; x++)
        {
            for (int y = 0; y < LocalMap.Height; y++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
            }
        }

        host.Session.PlayerLocalPosition = new LocalCoord(5, 5);
        host.QueueIntent(GameIntent.MoveToSelected, 7, 5);

        host.Tick();
        Assert.Equal(new LocalCoord(6, 5), host.Session.PlayerLocalPosition);
        host.Tick();
        Assert.Equal(new LocalCoord(7, 5), host.Session.PlayerLocalPosition);
    }

    [Fact]
    public void EnterSelected_EntersLocalMapWhenPlayerArrives()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);

        host.QueueIntent(GameIntent.EnterSelected, 12, 10);

        host.Tick();
        Assert.Equal(GameViewMode.Overworld, host.Session.ViewMode);
        host.Tick();
        Assert.Equal(GameViewMode.LocalMap, host.Session.ViewMode);
    }

    private static SimulationHost CreateHost(ulong seed)
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository)
        {
            IsNewGame = true
        };
        host.Initialize();
        return host;
    }
}
