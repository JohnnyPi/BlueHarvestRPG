using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Input;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;

namespace Game.Simulation.Tests;

public class EscapeVictoryTests
{
    [Fact]
    public void LeavingEscapeLocalMap_CompletesRun()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.EscapeTarget);

        host.Session.PlayerWorldPosition = scenario.EscapeTarget.Value;
        host.Session.EnterWorldCell();
        host.Session.LeaveLocalMap();

        Assert.Equal(RunOutcome.Escaped, host.Session.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(host.Session.RunEndTitle));
        Assert.False(string.IsNullOrWhiteSpace(host.Session.RunEndSummary));
    }

    [Fact]
    public void ReachingEscapeTarget_CompletesRun()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.EscapeTarget);

        host.Session.PlayerWorldPosition = scenario.EscapeTarget.Value;
        ScenarioObjectiveTracker.Check(host.Session);

        Assert.Equal(RunOutcome.Escaped, host.Session.Outcome);
        Assert.Equal(EscapeEndingKind.Survival, host.Session.EscapeEnding);
        Assert.False(string.IsNullOrWhiteSpace(host.Session.RunEndTitle));
        Assert.False(string.IsNullOrWhiteSpace(host.Session.RunEndSummary));
    }

    [Fact]
    public void ReachingEscapeWithMysterySolved_UsesResolvedEnding()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        Assert.NotNull(scenario.MysteryTarget);
        Assert.NotNull(scenario.EscapeTarget);

        host.Session.PlayerWorldPosition = scenario.MysteryTarget.Value;
        host.Session.EnterWorldCell();
        host.Session.LeaveLocalMap();

        host.Session.PlayerWorldPosition = scenario.EscapeTarget.Value;
        ScenarioObjectiveTracker.Check(host.Session);

        Assert.Equal(RunOutcome.Escaped, host.Session.Outcome);
        Assert.Equal(EscapeEndingKind.Resolved, host.Session.EscapeEnding);
        Assert.Equal("Truth Escapes With You", host.Session.RunEndTitle);
    }

    [Fact]
    public void PlayerDeath_EndsRun()
    {
        SimulationHost host = CreateHost();
        host.Session.EnterWorldCell();
        host.Session.PlayerEntity.Health = CombatResolver.DefaultAttackDamage;

        var combat = new CombatResolver();
        Entity raptor = new Entity
        {
            Id = new EntityId(77),
            Kind = EntityKind.Raptor,
            WorldPosition = host.Session.PlayerWorldPosition,
            LocalPosition = host.Session.PlayerLocalPosition,
            MaxHealth = 20,
            Health = 20,
            IsActive = true
        };

        combat.TryAttack(host.Session, raptor, host.Session.PlayerEntity);

        Assert.Equal(RunOutcome.Dead, host.Session.Outcome);
        Assert.Equal("Lost on the Island", host.Session.RunEndTitle);
    }

    [Fact]
    public void CompletedRun_BlocksMovement()
    {
        SimulationHost host = CreateHost();
        RunScenario scenario = host.Session.RunScenario!;
        host.Session.PlayerWorldPosition = scenario.EscapeTarget!.Value;
        ScenarioObjectiveTracker.Check(host.Session);

        WorldCoord before = host.Session.PlayerWorldPosition;
        host.QueueIntent(GameIntent.MoveEast);
        host.Tick();

        Assert.Equal(before, host.Session.PlayerWorldPosition);
        Assert.False(host.HasPendingSimulationWork);
    }

    [Fact]
    public void RunOutcome_SurvivesSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(909UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository);
        RunScenario scenario = session.RunScenario!;
        session.PlayerWorldPosition = scenario.EscapeTarget!.Value;
        ScenarioObjectiveTracker.Check(session);

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "victory");

        bool loaded = saveManager.TryLoad(
            "victory",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules,
            TestSaveDefaults.RulesHash,
            out _,
            out GameSession loadedSession,
            out _,
            out _,
            out _);

        Assert.True(loaded);
        Assert.Equal(RunOutcome.Escaped, loadedSession.Outcome);
        Assert.Equal(EscapeEndingKind.Survival, loadedSession.EscapeEnding);
        Assert.False(string.IsNullOrWhiteSpace(loadedSession.RunEndSummary));

        Directory.Delete(saveDirectory, recursive: true);
    }

    [Fact]
    public void PressureThreats_AppearInEscapeSummary()
    {
        SimulationHost host = CreateHost(888UL);
        RunScenario scenario = host.Session.RunScenario!;
        if (scenario.EscapeTarget == host.Session.PlayerWorldPosition)
        {
            host.Session.PlayerWorldPosition = FindSafeOverworldCell(host);
        }

        RaisePressureToThreshold(host, threshold: 3);
        Assert.True(host.Session.FinaleThreats.Contains(FinaleThreatId.PowerFailure));
        Assert.False(host.Session.IsRunComplete);

        host.Session.PlayerWorldPosition = scenario.EscapeTarget!.Value;
        ScenarioObjectiveTracker.Check(host.Session);

        Assert.Contains("Finale complications", host.Session.RunEndSummary);
        Assert.Contains("dead power sectors", host.Session.RunEndSummary);
    }

    [Fact]
    public void RaptorEncounter_IsRememberedForFinale()
    {
        SimulationHost host = CreateHost();
        host.Session.EnterWorldCell();

        var raptor = new Entity
        {
            Id = new EntityId(88),
            Kind = EntityKind.Raptor,
            WorldPosition = host.Session.PlayerWorldPosition,
            LocalPosition = host.Session.PlayerLocalPosition,
            MaxHealth = 20,
            Health = 20,
            IsActive = true
        };

        var combat = new CombatResolver();
        combat.TryAttack(host.Session, host.Session.PlayerEntity, raptor);

        Assert.True(host.Session.FinaleThreats.Contains(FinaleThreatId.RaptorPack));
    }

    [Fact]
    public void FinaleThreats_SurviveSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(919UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository);
        session.FinaleThreats.Record(FinaleThreatId.RaptorPack);
        session.FinaleThreats.Record(FinaleThreatId.PowerFailure);

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "threats");

        bool loaded = saveManager.TryLoad(
            "threats",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules,
            TestSaveDefaults.RulesHash,
            out _,
            out GameSession loadedSession,
            out _,
            out _,
            out _);

        Assert.True(loaded);
        Assert.True(loadedSession.FinaleThreats.Contains(FinaleThreatId.RaptorPack));
        Assert.True(loadedSession.FinaleThreats.Contains(FinaleThreatId.PowerFailure));

        Directory.Delete(saveDirectory, recursive: true);
    }

    private static WorldCoord FindSafeOverworldCell(SimulationHost host)
    {
        RunScenario? scenario = host.Session.RunScenario;
        for (int y = 0; y < host.Overworld.Height; y++)
        {
            for (int x = 0; x < host.Overworld.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (!host.Session.CanEnterOverworldCoord(coord))
                {
                    continue;
                }

                if (scenario?.EscapeTarget == coord || scenario?.MysteryTarget == coord)
                {
                    continue;
                }

                return coord;
            }
        }

        return host.Session.PlayerWorldPosition;
    }

    private static SimulationHost CreateHost(ulong seed = 777UL)
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository) { IsNewGame = true };
    }

    private static void RaisePressureToThreshold(SimulationHost host, int threshold)
    {
        int targetPressure = threshold * 20;
        while (host.Session.PressureClock.Pressure < targetPressure)
        {
            host.Session.AdvancePressureClock(100);
        }
    }
}
