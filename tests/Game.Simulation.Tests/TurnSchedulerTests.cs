using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Input;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class TurnSchedulerTests
{
    [Fact]
    public void PlayerWait_AdvancesCreaturePosition()
    {
        SimulationHost host = CreateLocalHostWithCreature(out Entity creature, out LocalCoord startPosition);

        host.QueueIntent(GameIntent.Wait);
        host.Tick();

        Assert.NotEqual(startPosition, creature.LocalPosition);
        Assert.True(host.Clock.WorldTime > 0);
    }

    [Fact]
    public void FasterCreature_AccumulatesMoreEnergyDuringPlayerRecovery()
    {
        int normalEnergy = MeasureCreatureEnergyAfterRecovery(creatureSpeed: 100);
        int fastEnergy = MeasureCreatureEnergyAfterRecovery(creatureSpeed: 120);

        Assert.True(fastEnergy > normalEnergy);
    }

    private static int MeasureCreatureEnergyAfterRecovery(int creatureSpeed)
    {
        var overworld = new OverworldGenerator().Generate(64, 64, 42UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        WorldCoord forestCell = FindBiomeCell(overworld, BiomeId.Forest);
        session.PlayerWorldPosition = forestCell;
        session.EnterWorldCell();

        LocalMap map = session.ActiveLocalMap!;
        ClearMapTerrain(map);
        map.Entities.ReplaceAll([]);

        var creature = new Entity
        {
            Id = new EntityId(500),
            Kind = EntityKind.WanderingCreature,
            WorldPosition = map.WorldPosition,
            LocalPosition = new LocalCoord(10, 10),
            BlocksMovement = true,
            IsActive = true,
            Actor = new ActorTurnState
            {
                Speed = creatureSpeed,
                Energy = 0
            }
        };
        map.Entities.Add(creature);
        session.PlayerTurnState.Energy = 0;

        var scheduler = new TurnScheduler();
        scheduler.RunUntilPlayerReady(session, static (_, _) => false);

        return creature.Actor!.Energy;
    }

    [Fact]
    public void BuildRenderSnapshot_WithoutTick_DoesNotAdvanceSimulation()
    {
        SimulationHost host = CreateLocalHostWithCreature(out Entity creature, out LocalCoord startPosition);
        long initialWorldTime = host.Clock.WorldTime;
        int initialEnergy = host.Session.PlayerTurnState.Energy;

        for (int i = 0; i < 20; i++)
        {
            host.BuildRenderSnapshot();
        }

        Assert.Equal(initialWorldTime, host.Clock.WorldTime);
        Assert.Equal(startPosition, creature.LocalPosition);
        Assert.Equal(initialEnergy, host.Session.PlayerTurnState.Energy);
    }

    [Fact]
    public void Tick_WithNoPendingWork_DoesNotAdvanceActionTickCount()
    {
        SimulationHost host = CreateHost();
        host.Initialize();

        host.Tick();

        RenderSnapshot snapshot = host.BuildRenderSnapshot();
        Assert.Equal(0, snapshot.TickCount);
        Assert.Equal(0, host.Clock.WorldTime);
    }

    [Fact]
    public void Wait_OnLocalMap_DoesNotSpendTravelStamina()
    {
        SimulationHost host = CreateLocalHostWithCreature(out _, out _);
        int staminaBefore = host.Session.PlayerTurnState.Energy;

        host.QueueIntent(GameIntent.Wait);
        host.Tick();

        Assert.Equal(staminaBefore, host.Session.PlayerTurnState.Energy);
        Assert.True(host.Clock.WorldTime > 0);
    }

    private static SimulationHost CreateLocalHostWithCreature(
        out Entity creature,
        out LocalCoord startPosition,
        ulong seed = 300UL)
    {
        SimulationHost host = CreateHost(seed);
        WorldCoord forestCell = FindBiomeCell(host.Overworld, BiomeId.Forest);
        host.Session.PlayerWorldPosition = forestCell;
        host.Session.EnterWorldCell();

        LocalMap map = host.Session.ActiveLocalMap!;
        ClearMapTerrain(map);

        creature = map.Entities.All.First(entity => entity.Kind == EntityKind.Raptor);
        creature.Actor ??= new ActorTurnState();
        creature.LocalPosition = new LocalCoord(10, 10);
        creature.Actor.Energy = ActionCostTable.ActionThreshold;
        startPosition = creature.LocalPosition;

        host.Session.PlayerLocalPosition = new LocalCoord(20, 20);
        return host;
    }

    private static void ClearMapTerrain(LocalMap map)
    {
        for (int y = 0; y < LocalMap.Height; y++)
        {
            for (int x = 0; x < LocalMap.Width; x++)
            {
                map.SetTerrain(x, y, TerrainId.Grass, TileFlags.None);
            }
        }
    }

    private static WorldCoord FindBiomeCell(Overworld overworld, BiomeId biome)
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

        throw new InvalidOperationException($"No {biome} cell found in overworld.");
    }

    private static SimulationHost CreateHost(ulong seed = 1UL)
    {
        var overworld = new OverworldGenerator().Generate(64, 64, seed);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}
