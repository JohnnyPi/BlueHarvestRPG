using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Input;
using Game.Simulation.Items;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class ScenarioGameplayTests
{
    [Fact]
    public void Generate_BindsObstacleTargets()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(5150UL);
        RunScenario scenario = ScenarioGenerator.Generate(overworld.Seed, overworld.IslandPlan);

        Assert.NotNull(scenario.Obstacle1Target);
        Assert.NotNull(scenario.Obstacle2Target);
        Assert.NotEqual(scenario.Obstacle1Target, scenario.Obstacle2Target);
    }

    [Fact]
    public void ObstacleCell_BlocksTravelUntilMysterySolved()
    {
        SimulationHost host = CreateHost(5150UL);
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.Obstacle1Target);

        host.Session.PlayerWorldPosition = new WorldCoord(
            scenario.Obstacle1Target.Value.X - 1,
            scenario.Obstacle1Target.Value.Y);

        Assert.False(host.Session.CanEnterOverworldStep(1, 0));

        host.Session.PlayerWorldPosition = scenario.MysteryTarget!.Value;
        host.Session.EnterWorldCell();
        Assert.True(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery));
        host.Session.LeaveLocalMap();

        host.Session.PlayerWorldPosition = new WorldCoord(
            scenario.Obstacle1Target.Value.X - 1,
            scenario.Obstacle1Target.Value.Y);
        Assert.True(host.Session.CanEnterOverworldStep(1, 0));
    }

    [Fact]
    public void FirstEncounter_TriggersOnFirstLocalEntry()
    {
        SimulationHost host = CreateHost(5150UL);
        Assert.False(host.Session.FirstEncounterTriggered);

        host.Session.EnterWorldCell();

        Assert.True(host.Session.FirstEncounterTriggered);
        Assert.Contains(host.Session.MessageLog.Recent(10), m => m.StartsWith("First encounter:"));
    }

    [Fact]
    public void Harvest_CostsEnergy()
    {
        SimulationHost host = CreateHost(100UL);
        WorldCoord landCell = FindLandCell(host.Overworld);
        host.Session.PlayerWorldPosition = landCell;
        host.Session.EnterWorldCell();

        Entity tree = host.Session.ActiveLocalMap!.Entities.All
            .First(entity => entity.Kind == EntityKind.HarvestableTree);
        int energyBefore = host.Session.PlayerTurnState.Energy;

        host.QueueIntent(GameIntent.HarvestAtSelected, tree.LocalPosition.X, tree.LocalPosition.Y);
        host.Tick();

        Assert.Equal(energyBefore - ActionCostTable.Harvest, host.Session.PlayerTurnState.Energy);
    }

    [Fact]
    public void Harvest_AddsWoodAndSometimesBerries()
    {
        SimulationHost host = CreateHost(100UL);
        WorldCoord landCell = FindLandCell(host.Overworld);
        host.Session.PlayerWorldPosition = landCell;
        host.Session.EnterWorldCell();

        Entity tree = host.Session.ActiveLocalMap!.Entities.All
            .First(entity => entity.Kind == EntityKind.HarvestableTree);
        host.Session.PlayerTurnState.Energy = ActionCostTable.MaxEnergy;

        host.QueueIntent(GameIntent.HarvestAtSelected, tree.LocalPosition.X, tree.LocalPosition.Y);
        host.Tick();

        Assert.Contains(host.Session.Inventory.Stacks, stack => stack.ItemId == ItemId.Wood && stack.Count >= 2);
    }

    [Fact]
    public void CanHarvestAt_RequiresHarvestableTreeEntity()
    {
        SimulationHost host = CreateHost(100UL);
        host.Session.EnterWorldCell();

        Entity tree = host.Session.ActiveLocalMap!.Entities.All
            .First(entity => entity.Kind == EntityKind.HarvestableTree);

        Assert.True(host.Session.CanHarvestAt(tree.LocalPosition.X, tree.LocalPosition.Y));
        Assert.False(host.Session.CanHarvestAt(0, 0));
    }

    private static WorldCoord FindLandCell(Overworld overworld)
    {
        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (overworld.IslandPlan?.IsLand(x, y) == true)
                {
                    return coord;
                }
            }
        }

        throw new InvalidOperationException("No land cell found.");
    }

    private static SimulationHost CreateHost(ulong seed)
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }
}
