using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.AI;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.LocalMaps;
using Game.Simulation.Pathfinding;
using Game.Simulation.Session;
using Game.Simulation.Factions;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class EntityPersistenceTests
{
    [Fact]
    public void RaptorFaction_SurvivesSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(606UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        WorldCoord forestCell = FindForestCell(overworld);
        var session = new GameSession(overworld, repository) { PlayerWorldPosition = forestCell };
        session.EnterWorldCell();

        Entity raptor = session.ActiveLocalMap!.Entities.All.First(entity => entity.Kind == EntityKind.Raptor);
        Assert.Equal(FactionId.Wildlife, raptor.Faction);

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "faction");

        bool loaded = saveManager.TryLoad(
            "faction",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out _,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _);

        Assert.True(loaded);
        Entity? loadedRaptor = loadedRepository
            .GetOrGenerate(forestCell)
            .Entities
            .GetById(raptor.Id);

        Assert.NotNull(loadedRaptor);
        Assert.Equal(FactionId.Wildlife, loadedRaptor!.Faction);

        Directory.Delete(saveDirectory, recursive: true);
    }

    [Fact]
    public void PlayerHealth_SurvivesOverworldSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(707UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository);
        session.EnterWorldCell();
        session.PlayerEntity.Health = 42;
        session.RefreshPlayerVitals();
        session.LeaveLocalMap();

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "health");

        bool loaded = saveManager.TryLoad(
            "health",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out _,
            out GameSession loadedSession,
            out _,
            out _);

        Assert.True(loaded);
        Assert.Equal(42, loadedSession.PlayerHealth);
        Assert.Equal(100, loadedSession.PlayerMaxHealth);

        Directory.Delete(saveDirectory, recursive: true);
    }

    private static WorldCoord FindForestCell(Overworld overworld)
    {
        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (overworld.GetCellValue(coord).Biome is BiomeId.Forest or BiomeId.Jungle)
                {
                    return coord;
                }
            }
        }

        throw new InvalidOperationException("No forest cell found.");
    }

    [Fact]
    public void SpawnDefaults_CreatesHarvestableTreeOnWalkableMaps()
    {
        SimulationHost host = CreateHost(100UL);
        WorldCoord landCell = FindLandCell(host.Overworld);
        host.Session.PlayerWorldPosition = landCell;
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        Entity? tree = map.Entities.All.FirstOrDefault(entity => entity.Kind == EntityKind.HarvestableTree);

        Assert.NotNull(tree);
        Assert.NotEqual(EntityId.Player, tree!.Id);
        Assert.True(tree.BlocksMovement);
        Assert.Equal(map.WorldPosition, tree.WorldPosition);
    }

    [Fact]
    public void SpawnDefaults_CreatesRaptorOnForestMaps()
    {
        SimulationHost host = CreateHost(100UL);
        WorldCoord forestCell = FindBiomeCell(host.Overworld, BiomeId.Forest, BiomeId.Jungle);
        host.Session.PlayerWorldPosition = forestCell;
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        Entity? raptor = map.Entities.All.FirstOrDefault(entity => entity.Kind == EntityKind.Raptor);

        Assert.NotNull(raptor);
        Assert.True(raptor!.BlocksMovement);
        Assert.NotNull(raptor.Raptor);
        Assert.Equal(RaptorPhase.Stalk, raptor.Raptor!.Phase);
    }

    [Fact]
    public void SpawnDefaults_CreatesTwoRaptorsOnJungleMaps()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(100UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        WorldCoord jungleCell = FindBiomeCell(overworld, BiomeId.Jungle);
        session.PlayerWorldPosition = jungleCell;
        session.EnterWorldCell();

        LocalMap map = session.ActiveLocalMap!;
        int raptorCount = map.Entities.All.Count(entity => entity.Kind == EntityKind.Raptor);

        Assert.True(raptorCount >= 2, $"Expected at least 2 jungle raptors, found {raptorCount}.");
    }

    [Fact]
    public void Entity_SurvivesLeaveAndReenter()
    {
        SimulationHost host = CreateHost(200UL);
        WorldCoord landCell = FindLandCell(host.Overworld);
        host.Session.PlayerWorldPosition = landCell;
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        Entity tree = map.Entities.All.First(entity => entity.Kind == EntityKind.HarvestableTree);
        EntityId treeId = tree.Id;
        LocalCoord treePosition = tree.LocalPosition;

        host.Session.LeaveLocalMap();
        host.Session.EnterWorldCell();

        Entity? restored = host.Session.ActiveLocalMap!.Entities.GetById(treeId);
        Assert.NotNull(restored);
        Assert.Equal(treePosition, restored!.LocalPosition);
        Assert.Equal(EntityKind.HarvestableTree, restored.Kind);
    }

    [Fact]
    public void Entity_SurvivesSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(555UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        WorldCoord landCell = FindLandCell(overworld);
        var session = new GameSession(overworld, repository)
        {
            PlayerWorldPosition = landCell
        };

        session.EnterWorldCell();
        LocalMap map = session.ActiveLocalMap!;
        Entity tree = map.Entities.All.First(entity => entity.Kind == EntityKind.HarvestableTree);
        EntityId treeId = tree.Id;
        LocalCoord treePosition = tree.LocalPosition;

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "entities");

        bool loaded = saveManager.TryLoad(
            "entities",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out Overworld loadedWorld,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _);

        Assert.True(loaded);
        LocalMap loadedMap = loadedRepository.GetOrGenerate(landCell);
        Entity? restored = loadedMap.Entities.GetById(treeId);

        Assert.NotNull(restored);
        Assert.Equal(treePosition, restored!.LocalPosition);
        Assert.Equal(EntityKind.HarvestableTree, restored.Kind);

        Directory.Delete(saveDirectory, recursive: true);
    }

    [Fact]
    public void FindPath_RespectsEntityBlocksMovement()
    {
        var map = new LocalMap(new WorldCoord(0, 0));

        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
            }
        }

        map.Entities.Add(new Entity
        {
            Id = new EntityId(42),
            Kind = EntityKind.HarvestableTree,
            WorldPosition = map.WorldPosition,
            LocalPosition = new LocalCoord(1, 0),
            BlocksMovement = true,
            IsActive = true
        });

        List<(int X, int Y)> path = GridPathfinder.FindPath(
            0,
            0,
            2,
            0,
            LocalMap.Width,
            LocalMap.Height,
            (x, y) => map.BlocksMovement(new LocalCoord(x, y)));

        Assert.NotEmpty(path);
        Assert.Equal((2, 0), path[^1]);
        Assert.DoesNotContain((1, 0), path);
    }

    [Fact]
    public void PlayerEntity_ReflectsSessionPosition()
    {
        SimulationHost host = CreateHost(42UL);
        host.Session.PlayerWorldPosition = new WorldCoord(8, 9);
        host.Session.PlayerLocalPosition = new LocalCoord(11, 22);

        Entity player = host.Session.PlayerEntity;

        Assert.Equal(EntityId.Player, player.Id);
        Assert.Equal(EntityKind.Player, player.Kind);
        Assert.Equal(new WorldCoord(8, 9), player.WorldPosition);
        Assert.Equal(new LocalCoord(11, 22), player.LocalPosition);
    }

    [Fact]
    public void CreateDeterministicId_IsStableForSameSeedAndCoordinate()
    {
        var coordinate = new WorldCoord(5, 7);

        EntityId first = EntityFactory.CreateDeterministicId(99UL, coordinate, EntityKind.HarvestableTree);
        EntityId second = EntityFactory.CreateDeterministicId(99UL, coordinate, EntityKind.HarvestableTree);

        Assert.Equal(first, second);
        Assert.NotEqual(EntityId.Player, first);
    }

    private static WorldCoord FindBiomeCell(Overworld overworld, params BiomeId[] biomes)
    {
        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (biomes.Contains(overworld.GetCellValue(coord).Biome))
                {
                    return coord;
                }
            }
        }

        throw new InvalidOperationException($"No matching biome cell found in overworld.");
    }

    private static WorldCoord FindLandCell(Overworld overworld)
    {
        if (overworld.IslandPlan is not null)
        {
            for (int y = 0; y < overworld.Height; y++)
            {
                for (int x = 0; x < overworld.Width; x++)
                {
                    if (overworld.IslandPlan.IsLand(x, y) && !overworld.IslandPlan.GetCell(x, y).IsCoast)
                    {
                        return new WorldCoord(x, y);
                    }
                }
            }
        }

        return FindBiomeCell(
            overworld,
            BiomeId.Plains,
            BiomeId.Forest,
            BiomeId.Jungle,
            BiomeId.Hills);
    }

    private static SimulationHost CreateHost(ulong seed)
    {
        var overworld = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}
