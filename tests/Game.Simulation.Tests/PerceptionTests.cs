using Game.Simulation.AI;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Perception;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.Visibility;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class PerceptionTests
{
    [Fact]
    public void TreeBlocksSight_KeepsRaptorUnaware()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        map.SetTerrain(5, 5, TerrainId.Grass, TileFlags.None);
        map.SetTerrain(6, 5, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);
        map.SetTerrain(7, 5, TerrainId.Grass, TileFlags.None);

        session.PlayerLocalPosition = new LocalCoord(7, 5);
        Entity raptor = CreateRaptor(map, new LocalCoord(5, 5));

        PerceptionSystem.Update(raptor, session, map, worldTime: 1);

        Assert.Equal(AwarenessLevel.Unaware, raptor.Perception!.Awareness);
    }

    [Fact]
    public void WalkNoise_WithoutSight_CreatesSuspicion()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        session.PlayerLocalPosition = new LocalCoord(8, 5);
        Entity raptor = CreateRaptor(map, new LocalCoord(5, 5));
        map.SetTerrain(7, 5, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);

        map.Noise.Emit(map, new LocalCoord(8, 5), NoiseEmitter.HarvestNoise, worldTime: 1);

        PerceptionSystem.Update(raptor, session, map, worldTime: 1);

        Assert.True(raptor.Perception!.Awareness >= AwarenessLevel.Suspicious);
    }

    [Fact]
    public void ScentTrail_CanBeDetectedWithoutSight()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        session.PlayerLocalPosition = new LocalCoord(8, 5);
        Entity raptor = CreateRaptor(map, new LocalCoord(5, 5));
        map.SetTerrain(7, 5, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);

        PerceptionSystem.DepositScent(map, new LocalCoord(7, 5), amount: 4);
        PerceptionSystem.DepositScent(map, new LocalCoord(8, 5), amount: 4);

        PerceptionSystem.Update(raptor, session, map, worldTime: 1);

        Assert.True(raptor.Perception!.Awareness >= AwarenessLevel.Suspicious);
    }

    [Fact]
    public void EngagedRaptor_LosesSight_DropsToTracking()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        session.PlayerLocalPosition = new LocalCoord(7, 5);
        Entity raptor = CreateRaptor(map, new LocalCoord(5, 5));
        raptor.Perception = new PerceptionState
        {
            Awareness = AwarenessLevel.Engaged,
            LastKnownPosition = session.PlayerLocalPosition
        };

        PerceptionSystem.Update(raptor, session, map, worldTime: 1);
        PerceptionSystem.Update(raptor, session, map, worldTime: 2);
        PerceptionSystem.Update(raptor, session, map, worldTime: 3);

        map.SetTerrain(6, 5, TerrainId.Tree, TileFlags.BlocksMovement | TileFlags.BlocksVision);

        PerceptionSystem.Update(raptor, session, map, worldTime: 4);
        PerceptionSystem.Update(raptor, session, map, worldTime: 5);
        PerceptionSystem.Update(raptor, session, map, worldTime: 6);

        Assert.Equal(AwarenessLevel.Tracking, raptor.Perception!.Awareness);
    }

    [Fact]
    public void ComputeVisible_DoesNotMutateExplored()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        var visible = new bool[map.Explored.Length];
        Array.Clear(map.Explored, 0, map.Explored.Length);

        FovCalculator.ComputeVisible(map, new LocalCoord(5, 5), radius: 5, visible);

        Assert.Contains(true, visible);
        Assert.DoesNotContain(true, map.Explored);
    }

    private static Entity CreateRaptor(LocalMap map, LocalCoord position)
    {
        var raptor = new Entity
        {
            Id = new EntityId(7001),
            Kind = EntityKind.Raptor,
            WorldPosition = map.WorldPosition,
            LocalPosition = position,
            BlocksMovement = true,
            IsActive = true,
            Faction = FactionId.Wildlife,
            Actor = new ActorTurnState { Speed = 130, Energy = ActionCostTable.ActionThreshold },
            MaxHealth = 24,
            Health = 24,
            Perception = new PerceptionState(),
            Raptor = new RaptorMemory { Phase = RaptorPhase.Stalk }
        };
        map.Entities.Add(raptor);
        return raptor;
    }

    private static GameSession CreateSession()
    {
        var overworld = new OverworldGenerator().Generate(64, 64, 42UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        WorldCoord forestCell = FindBiomeCell(overworld, BiomeId.Forest);
        session.PlayerWorldPosition = forestCell;
        session.EnterWorldCell();
        return session;
    }

    private static void ClearTerrain(LocalMap map)
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
}
