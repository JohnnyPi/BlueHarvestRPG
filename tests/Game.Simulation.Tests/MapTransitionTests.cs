using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Saves;
using Game.Persistence.Repositories;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class MapTransitionResolverTests
{
    [Theory]
    [InlineData(1, 0, 11, 8, 0, 24)]
    [InlineData(-1, 0, 9, 8, 63, 24)]
    [InlineData(0, -1, 10, 7, 24, 63)]
    [InlineData(0, 1, 10, 9, 24, 0)]
    public void TryResolve_MapsEdgeCrossingToMirroredPosition(
        int deltaX,
        int deltaY,
        int expectedWorldX,
        int expectedWorldY,
        int expectedLocalX,
        int expectedLocalY)
    {
        var overworld = new Overworld(64, 64, 1UL);
        var currentWorld = new WorldCoord(10, 8);
        var currentLocal = new LocalCoord(
            deltaX > 0 ? LocalMap.Width - 1 : deltaX < 0 ? 0 : 24,
            deltaY > 0 ? LocalMap.Height - 1 : deltaY < 0 ? 0 : 24);

        bool resolved = MapTransitionResolver.TryResolve(
            overworld,
            currentWorld,
            currentLocal,
            deltaX,
            deltaY,
            out MapTransition transition);

        Assert.True(resolved);
        Assert.Equal(new WorldCoord(expectedWorldX, expectedWorldY), transition.DestinationWorld);
        Assert.Equal(new LocalCoord(expectedLocalX, expectedLocalY), transition.DestinationLocal);
    }

    [Fact]
    public void TryResolve_ReturnsFalseForInBoundsMovement()
    {
        var overworld = new Overworld(64, 64, 1UL);

        bool resolved = MapTransitionResolver.TryResolve(
            overworld,
            new WorldCoord(10, 8),
            new LocalCoord(32, 32),
            1,
            0,
            out MapTransition transition);

        Assert.False(resolved);
        Assert.Equal(default(MapTransition), transition);
    }

    [Theory]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    [InlineData(63, 63, 1, 0)]
    [InlineData(63, 63, 0, 1)]
    public void TryResolve_BlocksTransitionOutsideOverworld(
        int worldX,
        int worldY,
        int deltaX,
        int deltaY)
    {
        var overworld = new Overworld(64, 64, 1UL);
        var currentWorld = new WorldCoord(worldX, worldY);
        var currentLocal = new LocalCoord(
            deltaX < 0 ? 0 : LocalMap.Width - 1,
            deltaY < 0 ? 0 : LocalMap.Height - 1);

        bool resolved = MapTransitionResolver.TryResolve(
            overworld,
            currentWorld,
            currentLocal,
            deltaX,
            deltaY,
            out _);

        Assert.False(resolved);
    }
}

public class MapTransitionTests
{
    [Theory]
    [InlineData(1, 0, 11, 8, 0, 24)]
    [InlineData(-1, 0, 9, 8, 63, 24)]
    [InlineData(0, -1, 10, 7, 24, 63)]
    [InlineData(0, 1, 10, 9, 24, 0)]
    public void TryMoveLocal_CrossesToNeighboringCell(
        int deltaX,
        int deltaY,
        int expectedWorldX,
        int expectedWorldY,
        int expectedLocalX,
        int expectedLocalY)
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.PlayerWorldPosition = new WorldCoord(10, 8);
        host.Session.EnterWorldCell();

        int edgeX = deltaX > 0 ? LocalMap.Width - 1 : deltaX < 0 ? 0 : 24;
        int edgeY = deltaY > 0 ? LocalMap.Height - 1 : deltaY < 0 ? 0 : 24;
        var edgePosition = new LocalCoord(edgeX, edgeY);
        PrepareWalkableTransition(
            host.Overworld,
            host.LocalMapRepository,
            host.Session.PlayerWorldPosition,
            edgePosition,
            deltaX,
            deltaY);

        host.Session.PlayerLocalPosition = edgePosition;
        LocalMap originMap = host.Session.ActiveLocalMap!;

        Assert.True(host.Session.TryMoveLocal(deltaX, deltaY));
        Assert.Equal(GameViewMode.LocalMap, host.Session.ViewMode);
        Assert.Equal(new WorldCoord(expectedWorldX, expectedWorldY), host.Session.PlayerWorldPosition);
        Assert.Equal(new LocalCoord(expectedLocalX, expectedLocalY), host.Session.PlayerLocalPosition);
        Assert.NotSame(originMap, host.Session.ActiveLocalMap);
    }

    [Fact]
    public void TryMoveLocal_BlocksTransitionWhenDestinationTileBlocksMovement()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.PlayerWorldPosition = new WorldCoord(10, 8);
        host.Session.EnterWorldCell();

        const int edgeY = 24;
        var edgePosition = new LocalCoord(LocalMap.Width - 1, edgeY);
        host.Session.PlayerLocalPosition = edgePosition;
        PrepareWalkableTransition(
            host.Overworld,
            host.LocalMapRepository,
            host.Session.PlayerWorldPosition,
            edgePosition,
            1,
            0);

        LocalMap neighbor = host.LocalMapRepository.GetOrGenerateSurface(new WorldCoord(11, 8));
        neighbor.SetTerrain(0, edgeY, TerrainId.Rock, TileFlags.BlocksMovement);

        Assert.False(host.Session.TryMoveLocal(1, 0));
        Assert.Equal(new WorldCoord(10, 8), host.Session.PlayerWorldPosition);
        Assert.Equal(new LocalCoord(LocalMap.Width - 1, edgeY), host.Session.PlayerLocalPosition);
    }

    [Fact]
    public void TryMoveLocal_RoundTripPreservesTerrainMutations()
    {
        SimulationHost host = CreateHost(4242UL);
        host.Session.PlayerWorldPosition = new WorldCoord(10, 8);
        host.Session.EnterWorldCell();

        const int edgeY = 24;
        var edgePosition = new LocalCoord(LocalMap.Width - 1, edgeY);
        host.Session.PlayerLocalPosition = edgePosition;
        PrepareWalkableTransition(
            host.Overworld,
            host.LocalMapRepository,
            host.Session.PlayerWorldPosition,
            edgePosition,
            1,
            0);

        LocalMap originMap = host.Session.ActiveLocalMap!;
        originMap.SetTerrain(30, 30, TerrainId.Dirt, TileFlags.None);

        PrepareWalkableTransition(
            host.Overworld,
            host.LocalMapRepository,
            new WorldCoord(11, 8),
            new LocalCoord(0, edgeY),
            -1,
            0);

        Assert.True(host.Session.TryMoveLocal(1, 0));
        Assert.True(host.Session.TryMoveLocal(-1, 0));

        Assert.Same(originMap, host.Session.ActiveLocalMap);
        Assert.Equal(TerrainId.Dirt, originMap.Terrain[originMap.GetIndex(30, 30)]);
    }

    [Fact]
    public void EnterWorldCell_AcceptsExplicitEntryPoint()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.EnterWorldCell(new LocalCoord(5, 12));

        Assert.Equal(new LocalCoord(5, 12), host.Session.PlayerLocalPosition);
    }

    [Fact]
    public void SaveAndLoad_RestoresPositionAfterCrossCellTransition()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(777UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository)
        {
            PlayerWorldPosition = new WorldCoord(10, 8)
        };

        session.EnterWorldCell();
        const int edgeY = 24;
        var edgePosition = new LocalCoord(LocalMap.Width - 1, edgeY);
        session.PlayerLocalPosition = edgePosition;
        PrepareWalkableTransition(
            overworld,
            repository,
            session.PlayerWorldPosition,
            edgePosition,
            1,
            0);

        Assert.True(session.TryMoveLocal(1, 0));

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "transition");

        bool loaded = saveManager.TryLoad(
            "transition",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out Overworld loadedWorld,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _);

        Assert.True(loaded);
        Assert.Equal(777UL, loadedWorld.Seed);
        Assert.Equal(GameViewMode.LocalMap, loadedSession.ViewMode);
        Assert.Equal(new WorldCoord(11, 8), loadedSession.PlayerWorldPosition);
        Assert.Equal(new LocalCoord(0, edgeY), loadedSession.PlayerLocalPosition);
        Assert.NotNull(loadedSession.ActiveLocalMap);
        Assert.Equal(new WorldCoord(11, 8), loadedSession.ActiveLocalMap!.WorldPosition);

        Directory.Delete(saveDirectory, recursive: true);
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

    private static SimulationHost CreateHost(ulong seed)
    {
        var overworld = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}
