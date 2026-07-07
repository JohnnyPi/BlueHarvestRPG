using Game.Generation.Biomes;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;


public class BiomeClassifierTests
{
    [Theory]
    [InlineData(0.2f, 0.5f, 0.5f, BiomeId.Ocean)]
    [InlineData(0.37f, 0.5f, 0.5f, BiomeId.Beach)]
    [InlineData(0.9f, 0.5f, 0.5f, BiomeId.Mountains)]
    [InlineData(0.75f, 0.5f, 0.5f, BiomeId.Hills)]
    [InlineData(0.5f, 0.8f, 0.5f, BiomeId.Swamp)]
    [InlineData(0.5f, 0.6f, 0.5f, BiomeId.Forest)]
    [InlineData(0.5f, 0.2f, 0.5f, BiomeId.Plains)]
    public void ClassifyBiome_UsesExpectedThresholds(
        float elevation,
        float moisture,
        float temperature,
        BiomeId expected)
    {
        BiomeClassifier classifier = BiomeClassifier.CreateDefault();
        BiomeId biome = classifier.Classify(elevation, moisture, temperature);
        Assert.Equal(expected, biome);
    }
}

public class GenerationDeterminismTests
{
    [Fact]
    public void OverworldGenerator_ProducesIdenticalResultsForSameSeed()
    {
        var generator = new OverworldGenerator();
        const ulong seed = 12345UL;

        Overworld first = generator.Generate(64, 64, seed);
        Overworld second = generator.Generate(64, 64, seed);

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                var coord = new WorldCoord(x, y);
                WorldCell a = first.GetCellValue(coord);
                WorldCell b = second.GetCellValue(coord);

                Assert.Equal(a.Biome, b.Biome);
                Assert.Equal(a.Elevation, b.Elevation);
                Assert.Equal(a.Moisture, b.Moisture);
                Assert.Equal(a.Temperature, b.Temperature);
            }
        }
    }

    [Fact]
    public void LocalMapGenerator_ProducesIdenticalMapsForSameSeedAndCoordinate()
    {
        var overworldGenerator = new OverworldGenerator();
        var localGenerator = new LocalMapGenerator();
        const ulong seed = 98765UL;

        Overworld world = overworldGenerator.Generate(64, 64, seed);
        var coord = new WorldCoord(10, 12);

        LocalMap first = localGenerator.Generate(world, MapKey.Surface(coord));
        LocalMap second = localGenerator.Generate(world, MapKey.Surface(coord));

        Assert.Equal(first.Terrain, second.Terrain);
        Assert.Equal(first.Flags, second.Flags);
    }

    [Fact]
    public void SaveLoad_WithIslandDefinition_ProducesIdenticalOverworld()
    {
        var island = TestSaveDefaults.Island;
        uint hash = Game.Content.BiomeRulesHash.Compute(new Game.Content.Definitions.BiomeRulesDefinition());
        var generator = new IslandWorldGenerator(island);
        const ulong seed = 424242UL;

        Overworld original = generator.Generate(seed);

        string saveDirectory = Path.Combine(Path.GetTempPath(), "BlueHarvestTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var repository = new InMemoryLocalMapRepository(original, localGenerator);
        var session = new GameSession(original, repository);

        saveManager.Save(original, session, repository, hash, "determinism");

        bool loaded = saveManager.TryLoad(
            "determinism",
            localGenerator,
            island,
            hash,
            out Overworld loadedWorld,
            out _,
            out _,
            out _);

        Assert.True(loaded);
        Assert.Equal(seed, loadedWorld.Seed);
        Assert.NotNull(loadedWorld.IslandPlan);

        for (int y = 0; y < island.OverworldSize; y++)
        {
            for (int x = 0; x < island.OverworldSize; x++)
            {
                var coord = new WorldCoord(x, y);
                Assert.Equal(original.GetCellValue(coord).Biome, loadedWorld.GetCellValue(coord).Biome);
            }
        }

        Directory.Delete(saveDirectory, recursive: true);
    }
}

public class GameSessionTests
{
    [Fact]
    public void EnterWorldCell_SwitchesToLocalMapAndCentersPlayer()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.EnterWorldCell();

        Assert.Equal(GameViewMode.LocalMap, host.Session.ViewMode);
        Assert.NotNull(host.Session.ActiveLocalMap);
        var center = new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2);
        var expected = WalkabilityHelper.FindNearestWalkable(host.Session.ActiveLocalMap!, center);
        Assert.Equal(expected, host.Session.PlayerLocalPosition);
    }

    [Fact]
    public void LeaveLocalMap_ReturnsToOverworld()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.EnterWorldCell();
        host.Session.LeaveLocalMap();

        Assert.Equal(GameViewMode.Overworld, host.Session.ViewMode);
        Assert.Null(host.Session.ActiveLocalMap);
    }

    [Fact]
    public void TryMoveOverworld_StaysInsideBounds()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.PlayerWorldPosition = new WorldCoord(0, 0);

        Assert.False(host.Session.TryMoveOverworld(-1, 0));
        Assert.False(host.Session.TryMoveOverworld(0, -1));
        Assert.Equal(0, host.Session.PlayerWorldPosition.X);
        Assert.Equal(0, host.Session.PlayerWorldPosition.Y);
    }

    [Fact]
    public void TryMoveLocal_BlocksMovementThroughTrees()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        map.SetTerrain(32, 31, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);
        host.Session.PlayerLocalPosition = new LocalCoord(32, 32);

        Assert.False(host.Session.TryMoveLocal(0, -1));
        Assert.Equal(32, host.Session.PlayerLocalPosition.Y);
    }

    [Fact]
    public void TryRemoveTerrainAtPlayer_RemovesTreeAndPersistsAfterLeaveAndReenter()
    {
        SimulationHost host = CreateHost(4242UL);
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        host.Session.PlayerLocalPosition = new LocalCoord(10, 10);
        map.SetTerrain(10, 10, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);

        Assert.True(host.Session.TryRemoveTerrainAtPlayer());
        Assert.Equal(TerrainId.Grass, map.Terrain[map.GetIndex(10, 10)]);

        host.Session.LeaveLocalMap();
        host.Session.EnterWorldCell();

        LocalMap reloaded = host.Session.ActiveLocalMap!;
        Assert.Equal(TerrainId.Grass, reloaded.Terrain[reloaded.GetIndex(10, 10)]);
    }

    private static SimulationHost CreateHost(ulong seed)
    {
        var overworld = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}

public class PersistenceTests
{
    [Fact]
    public void SaveAndLoad_RestoresSeedPlayerPositionAndMutatedMap()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(555UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository)
        {
            PlayerWorldPosition = new WorldCoord(20, 21)
        };

        session.EnterWorldCell();
        session.PlayerLocalPosition = new LocalCoord(5, 5);
        LocalMap map = session.ActiveLocalMap!;
        map.SetTerrain(5, 5, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);
        session.TryRemoveTerrainAtPlayer();
        session.LeaveLocalMap();

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "test");

        bool loaded = saveManager.TryLoad(
            "test",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out Overworld loadedWorld,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _);

        Assert.True(loaded);
        Assert.Equal(555UL, loadedWorld.Seed);
        Assert.Equal(new WorldCoord(20, 21), loadedSession.PlayerWorldPosition);
        Assert.Equal(GameViewMode.Overworld, loadedSession.ViewMode);

        loadedSession.EnterWorldCell();
        LocalMap loadedMap = loadedSession.ActiveLocalMap!;
        Assert.Equal(TerrainId.Grass, loadedMap.Terrain[loadedMap.GetIndex(5, 5)]);

        Directory.Delete(saveDirectory, recursive: true);
    }
}
