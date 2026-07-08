using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Input;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class DirectionResolverTests
{
    [Theory]
    [InlineData(0, -1, Direction.North)]
    [InlineData(0, 1, Direction.South)]
    [InlineData(1, 0, Direction.East)]
    [InlineData(-1, 0, Direction.West)]
    [InlineData(0, -2, Direction.North)]
    [InlineData(0, 2, Direction.South)]
    public void TryFromDelta_MapsCardinalMoves(int deltaX, int deltaY, Direction expected)
    {
        Assert.True(DirectionResolver.TryFromDelta(deltaX, deltaY, out Direction direction));
        Assert.Equal(expected, direction);
    }

    [Theory]
    [InlineData(1, -1, Direction.North)]
    [InlineData(-1, -1, Direction.North)]
    [InlineData(1, 1, Direction.South)]
    [InlineData(-1, 1, Direction.South)]
    [InlineData(1, -2, Direction.North)]
    [InlineData(2, -1, Direction.East)]
    [InlineData(2, 1, Direction.East)]
    [InlineData(-2, 1, Direction.West)]
    public void TryFromDelta_MapsDiagonalMovesToDominantAxis(int deltaX, int deltaY, Direction expected)
    {
        Assert.True(DirectionResolver.TryFromDelta(deltaX, deltaY, out Direction direction));
        Assert.Equal(expected, direction);
    }

    [Fact]
    public void TryFromDelta_ReturnsFalseForZeroDelta()
    {
        Assert.False(DirectionResolver.TryFromDelta(0, 0, out _));
    }
}

public class PlayerFacingTests
{
    [Fact]
    public void NewSession_DefaultsFacingSouth()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        Assert.Equal(Direction.South, host.Session.PlayerFacing);
    }

    [Fact]
    public void OverworldMove_UpdatesFacing()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 650;

        host.QueueIntent(GameIntent.MoveEast);
        host.Tick();

        Assert.Equal(Direction.East, host.Session.PlayerFacing);
    }

    [Fact]
    public void BlockedOverworldMove_StillUpdatesFacing()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        ref WorldCell oceanCell = ref host.Overworld.GetCell(new WorldCoord(15, 10));
        oceanCell.Biome = BiomeId.Ocean;
        oceanCell.Elevation = 0.1f;

        host.Session.PlayerWorldPosition = new WorldCoord(14, 10);
        host.QueueIntent(GameIntent.MoveEast);
        host.Tick();

        Assert.Equal(Direction.East, host.Session.PlayerFacing);
        Assert.Equal(new WorldCoord(14, 10), host.Session.PlayerWorldPosition);
    }

    [Fact]
    public void LocalMove_UpdatesFacing()
    {
        SimulationHost host = CreateLocalHost();
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
        host.QueueIntent(GameIntent.MoveNorth);
        host.Tick();

        Assert.Equal(Direction.North, host.Session.PlayerFacing);
    }

    [Fact]
    public void QueuedPath_UpdatesFacingEachStep()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 650;

        host.QueueIntent(GameIntent.MoveToSelected, 12, 10);
        host.Tick();
        Assert.Equal(Direction.East, host.Session.PlayerFacing);
        Assert.Equal(new WorldCoord(11, 10), host.Session.PlayerWorldPosition);

        host.Tick();
        Assert.Equal(Direction.East, host.Session.PlayerFacing);
        Assert.Equal(new WorldCoord(12, 10), host.Session.PlayerWorldPosition);
    }

    [Fact]
    public void BuildRenderSnapshot_IncludesPlayerFacing()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 650;
        host.Session.PlayerFacing = Direction.West;

        var snapshot = host.BuildRenderSnapshot();

        Assert.Equal(Direction.West, snapshot.PlayerFacing);
    }

    private static SimulationHost CreateLocalHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(7UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }
}
