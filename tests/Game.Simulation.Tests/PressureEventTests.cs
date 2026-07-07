using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Input;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;

namespace Game.Simulation.Tests;

public class PressureEventTests
{
    [Fact]
    public void Threshold2_IncreasesTravelStaminaCost()
    {
        SimulationHost host = CreateHost();
        int baseCost = host.Session.GetOverworldStepCost(1, 0);

        RaisePressureToThreshold(host, 2);

        Assert.True(host.Session.PressureState.TravelStaminaPenalty >= 10);
        Assert.True(host.Session.GetOverworldStepCost(1, 0) > baseCost);
    }

    [Fact]
    public void Threshold3_BlocksQueuedMovementThroughHazardousCell()
    {
        SimulationHost host = CreateHost();
        RaisePressureToThreshold(host, 3);

        Assert.NotNull(host.Session.PressureState.HazardousTravelCell);
        WorldCoord blocked = host.Session.PressureState.HazardousTravelCell!.Value;
        host.Session.PlayerWorldPosition = new WorldCoord(blocked.X - 1, blocked.Y);

        Assert.False(host.Session.QueueMoveTo(blocked.X, blocked.Y));
        Assert.False(host.Session.TryMoveOverworld(1, 0));
    }

    [Fact]
    public void Threshold3_BlocksHazardousTravelCell()
    {
        SimulationHost host = CreateHost();
        RaisePressureToThreshold(host, 3);

        Assert.NotNull(host.Session.PressureState.HazardousTravelCell);
        WorldCoord blocked = host.Session.PressureState.HazardousTravelCell!.Value;
        host.Session.PlayerWorldPosition = new WorldCoord(blocked.X - 1, blocked.Y);

        Assert.False(host.Session.CanEnterOverworldStep(1, 0));
    }

    [Fact]
    public void Threshold4_StartsEvacuationCountdown()
    {
        SimulationHost host = CreateHost();
        RaisePressureToThreshold(host, 4);

        Assert.Equal(PressureEventResolver.EvacWindowHours, host.Session.PressureState.EvacHoursRemaining);
    }

    [Fact]
    public void EvacCountdown_DecreasesWithWorldHours()
    {
        SimulationHost host = CreateHost();
        host.Session.PressureState.EvacHoursRemaining = 3;

        host.Session.NotifyWorldHourElapsed();
        host.Session.NotifyWorldHourElapsed();

        Assert.Equal(1, host.Session.PressureState.EvacHoursRemaining);
    }

    [Fact]
    public void Threshold1_OnLocalMap_SpawnsPredator()
    {
        SimulationHost host = CreateHost();
        host.Session.EnterWorldCell();
        int creaturesBefore = host.Session.ActiveLocalMap!.Entities.All.Count(e => e.Kind == EntityKind.Raptor);

        RaisePressureToThreshold(host, 1);

        int creaturesAfter = host.Session.ActiveLocalMap.Entities.All.Count(e => e.Kind == EntityKind.Raptor);
        Assert.True(creaturesAfter > creaturesBefore);
    }

    [Fact]
    public void Threshold1_OnOverworld_QueuesPredatorForNextEntry()
    {
        SimulationHost host = CreateHost();
        RaisePressureToThreshold(host, 1);

        Assert.True(host.Session.PressureState.PendingPredatorSpawn);

        host.Session.EnterWorldCell();
        Assert.False(host.Session.PressureState.PendingPredatorSpawn);
    }

    private static void RaisePressureToThreshold(SimulationHost host, int threshold)
    {
        int targetPressure = threshold * 20;
        while (host.Session.PressureClock.Pressure < targetPressure)
        {
            host.Session.AdvancePressureClock(100);
        }
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(555UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }
}
