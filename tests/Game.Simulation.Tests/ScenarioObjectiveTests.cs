using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Input;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class ScenarioObjectiveTests
{
    [Fact]
    public void Generate_BindsEscapeAndMysteryTargets()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        RunScenario scenario = ScenarioGenerator.Generate(overworld.Seed, overworld.IslandPlan);

        Assert.NotNull(scenario.EscapeTarget);
        Assert.NotNull(scenario.MysteryTarget);
        Assert.False(string.IsNullOrWhiteSpace(scenario.EscapeLandmark));
        Assert.False(string.IsNullOrWhiteSpace(scenario.MysteryLandmark));
    }

    [Fact]
    public void ReachingEscapeTarget_CompletesEscapeQuest()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.EscapeTarget);

        host.Session.PlayerWorldPosition = scenario.EscapeTarget.Value;
        ScenarioObjectiveTracker.Check(host.Session);

        Assert.True(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Escape));
    }

    [Fact]
    public void EnteringMysterySite_CompletesMysteryQuest()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.MysteryTarget);

        host.Session.PlayerWorldPosition = scenario.MysteryTarget.Value;
        host.Session.EnterWorldCell();

        Assert.True(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery));
    }

    [Fact]
    public void NewGame_StartsScenarioQuests()
    {
        SimulationHost host = CreateHost();

        Assert.False(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Escape));
        Assert.False(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Mystery));
        Assert.False(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Endure));
    }

    [Fact]
    public void PressureThreshold_CompletesEndureQuest()
    {
        SimulationHost host = CreateHost();

        for (int i = 0; i < 40; i++)
        {
            host.Session.AdvancePressureClock(100);
        }

        Assert.True(host.Session.QuestLog.IsCompleted(ScenarioQuestIds.Endure));
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(777UL);
        var repository = new InMemoryLocalMapRepository(overworld, new Game.Generation.LocalMaps.LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }
}
