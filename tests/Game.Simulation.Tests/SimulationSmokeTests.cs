using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Rendering;
using Game.Simulation.Session;

namespace Game.Simulation.Tests;

public class SimulationSmokeTests
{
    [Fact]
    public void Tick_DoesNotThrow()
    {
        SimulationHost host = CreateHost();
        host.Initialize();
        host.QueueIntent(Game.Simulation.Input.GameIntent.Wait);

        var exception = Record.Exception(() => host.Tick());

        Assert.Null(exception);
    }

    [Fact]
    public void BuildRenderSnapshot_ReturnsExpectedTitle()
    {
        SimulationHost host = CreateHost();
        host.Initialize();
        host.QueueIntent(Game.Simulation.Input.GameIntent.Wait);
        host.Tick();

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.Equal("Blue Harvest", snapshot.Title);
        Assert.Equal(GameViewMode.Overworld, snapshot.ViewMode);
        Assert.Equal(64, snapshot.GridWidth);
        Assert.Equal(1, snapshot.TickCount);
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new OverworldGenerator().Generate(64, 64, 1UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}
