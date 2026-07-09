using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Input;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class OverworldTravelTests
{
    [Fact]
    public void OverworldWalk_ReducesEnergyBelowStartingPool()
    {
        SimulationHost host = CreateHost();
        WorldCoord start = FindPassableNeighbor(host.Overworld, 1, 0);
        host.Session.PlayerWorldPosition = start;
        int initialEnergy = host.Session.PlayerTurnState.Energy;

        host.QueueIntent(GameIntent.MoveEast);
        host.Tick();

        Assert.True(host.Session.PlayerTurnState.Energy < initialEnergy);
        Assert.True(host.Clock.WorldTime > 0);
    }

    [Fact]
    public void Ocean_BlocksOverworldMovement()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        ref WorldCell oceanCell = ref host.Overworld.GetCell(new WorldCoord(15, 10));
        oceanCell.Biome = BiomeId.Ocean;
        oceanCell.Elevation = 0.1f;

        host.Session.PlayerWorldPosition = new WorldCoord(14, 10);
        int energyBefore = host.Session.PlayerTurnState.Energy;

        host.QueueIntent(GameIntent.MoveEast);
        host.Tick();

        Assert.Equal(new WorldCoord(14, 10), host.Session.PlayerWorldPosition);
        Assert.Equal(energyBefore, host.Session.PlayerTurnState.Energy);
    }

    [Fact]
    public void OverworldRest_RecoversEnoughForPlainsTravel()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 20;

        host.QueueIntent(GameIntent.Wait);
        host.Tick();

        Assert.True(host.Session.PlayerTurnState.Energy >= BiomeTraversal.GetMoveCost(BiomeId.Plains));
    }

    [Fact]
    public void OverworldWalk_AfterExhaustion_RestAllowsFurtherTravel()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 15;
        WorldCoord positionBeforeRest = host.Session.PlayerWorldPosition;

        host.QueueIntent(GameIntent.Wait);
        host.Tick();

        host.QueueIntent(GameIntent.MoveNorth);
        host.Tick();

        Assert.NotEqual(positionBeforeRest, host.Session.PlayerWorldPosition);
    }

    [Fact]
    public void ForestStep_CostsMoreThanBeach()
    {
        Assert.True(BiomeTraversal.GetMoveCost(BiomeId.Forest) > BiomeTraversal.GetMoveCost(BiomeId.Beach));
    }

    [Fact]
    public void LocalMap_DoesNotSpendTravelStamina()
    {
        SimulationHost host = TestOverworldFactory.CreatePlainsHost();
        host.Session.PlayerWorldPosition = new WorldCoord(10, 10);
        host.Session.PlayerTurnState.Energy = 15;

        host.Session.EnterWorldCell();
        Assert.Equal(GameViewMode.LocalMap, host.Session.ViewMode);

        int startY = host.Session.PlayerLocalPosition.Y;

        host.QueueIntent(GameIntent.MoveNorth);
        host.Tick();
        Assert.Equal(startY - 1, host.Session.PlayerLocalPosition.Y);
        Assert.Equal(15, host.Session.PlayerTurnState.Energy);

        host.QueueIntent(GameIntent.Wait);
        host.Tick();
        Assert.Equal(15, host.Session.PlayerTurnState.Energy);
    }

    [Fact]
    public void SaveLoad_PreservesOverworldEnergy()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "blueharvest-tests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        SimulationHost host = CreateHost();
        host.Session.PlayerTurnState.Energy = 237;
        host.Session.PlayerTurnState.EnergyRemainder = 4;

        saveManager.Save(host.Overworld, host.Session, (InMemoryLocalMapRepository)host.LocalMapRepository, TestSaveDefaults.RulesHash);

        bool loaded = saveManager.TryLoad(
            "autosave",
            new LocalMapGenerator(),
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules,
            TestSaveDefaults.RulesHash,
            out _,
            out GameSession loadedSession,
            out _,
            out _,
            out string? failureReason);

        Assert.True(loaded, failureReason);
        Assert.Equal(237, loadedSession.PlayerTurnState.Energy);
        Assert.Equal(4, loadedSession.PlayerTurnState.EnergyRemainder);
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }

    private static WorldCoord FindPassableNeighbor(Overworld overworld, int deltaX, int deltaY)
    {
        for (int y = 1; y < overworld.Height - 1; y++)
        {
            for (int x = 1; x < overworld.Width - 1; x++)
            {
                var from = new WorldCoord(x, y);
                var to = new WorldCoord(x + deltaX, y + deltaY);
                if (BiomeTraversal.IsPassable(overworld.GetCellValue(from).Biome) &&
                    BiomeTraversal.IsPassable(overworld.GetCellValue(to).Biome))
                {
                    return from;
                }
            }
        }

        throw new InvalidOperationException("No passable neighbor pair found.");
    }

    private static WorldCoord FindBiome(Overworld overworld, BiomeId biome)
    {
        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (overworld.GetCellValue(coord).Biome == biome)
                {
                    return coord;
                }
            }
        }

        throw new InvalidOperationException($"Biome {biome} not found.");
    }

    private static WorldCoord? FindPassableNeighborOf(Overworld overworld, WorldCoord target)
    {
        (int dx, int dy)[] deltas = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        foreach ((int dx, int dy) in deltas)
        {
            var neighbor = new WorldCoord(target.X + dx, target.Y + dy);
            if (!overworld.Contains(neighbor))
            {
                continue;
            }

            if (BiomeTraversal.IsPassable(overworld.GetCellValue(neighbor).Biome))
            {
                return neighbor;
            }
        }

        return null;
    }
}
