using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Persistence.Saves;
using Game.Simulation;
using Game.Simulation.AI;
using Game.Simulation.Combat;
using Game.Simulation.Coordinates;
using Game.Simulation.Entities;
using Game.Simulation.Factions;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.Time;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class RaptorBehaviorTests
{
    [Fact]
    public void OnDamaged_SetsRetreatPhaseAndCooldown()
    {
        GameSession session = CreateSession();
        Entity raptor = CreateRaptor(session, new LocalCoord(5, 5));

        RaptorBehavior.OnDamaged(session, raptor);

        Assert.Equal(RaptorPhase.Retreat, raptor.Raptor!.Phase);
        Assert.Equal(6, raptor.Raptor.AmbushCooldown);
        Assert.Contains(session.MessageLog.Messages, message => message.Contains("falls back"));
    }

    [Fact]
    public void PlayerAttack_TriggersRaptorRetreat()
    {
        GameSession session = CreateSession();
        Entity raptor = CreateRaptor(session, new LocalCoord(5, 5));
        session.PlayerLocalPosition = new LocalCoord(6, 5);

        var combat = new CombatResolver();
        bool attacked = combat.TryAttack(session, session.PlayerEntity, raptor);

        Assert.True(attacked);
        Assert.Equal(RaptorPhase.Retreat, raptor.Raptor!.Phase);
    }

    [Fact]
    public void Describe_ReflectsCurrentPhase()
    {
        Entity raptor = CreateRaptor(CreateSession(), new LocalCoord(1, 1));
        raptor.Raptor!.Phase = RaptorPhase.ProbeFence;

        Assert.Equal("Raptor (testing fence)", RaptorBehavior.Describe(raptor));
    }

    [Fact]
    public void FenceProbe_AnnouncesOnce()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        map.SetTerrain(5, 5, TerrainId.Grass, TileFlags.None);
        map.SetTerrain(6, 5, TerrainId.Fence, TileFlags.BlocksMovement);
        Entity raptor = CreateRaptor(session, new LocalCoord(5, 5));
        raptor.Raptor!.Phase = RaptorPhase.ProbeFence;

        RaptorBehavior.TryAct(raptor, session, map, worldTime: 0);
        RaptorBehavior.TryAct(raptor, session, map, worldTime: 1);

        int probeMessages = session.MessageLog.Messages.Count(message => message.Contains("tests the perimeter fence"));
        Assert.Equal(1, probeMessages);
    }

    [Fact]
    public void Retreat_AfterCooldown_TransitionsToAmbush()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        session.PlayerLocalPosition = new LocalCoord(20, 20);
        Entity raptor = CreateRaptor(session, new LocalCoord(10, 10));
        raptor.Raptor!.Phase = RaptorPhase.Retreat;
        raptor.Raptor.AmbushCooldown = 1;

        RaptorBehavior.TryAct(raptor, session, map, worldTime: 0);

        Assert.Equal(RaptorPhase.Ambush, raptor.Raptor.Phase);
        Assert.Contains(session.MessageLog.Messages, message => message.Contains("circles back"));
    }

    [Fact]
    public void Stalk_WhenPlayerIsClose_MovesAway()
    {
        GameSession session = CreateSession();
        LocalMap map = session.ActiveLocalMap!;
        ClearTerrain(map);

        session.PlayerLocalPosition = new LocalCoord(6, 5);
        Entity raptor = CreateRaptor(session, new LocalCoord(5, 5));
        LocalCoord start = raptor.LocalPosition;

        RaptorBehavior.TryAct(raptor, session, map, worldTime: 0);

        Assert.NotEqual(start, raptor.LocalPosition);
        Assert.True(Manhattan(raptor.LocalPosition, session.PlayerLocalPosition) > Manhattan(start, session.PlayerLocalPosition));
    }

    [Fact]
    public void RaptorMemory_SurvivesSaveAndLoad()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator();
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(888UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        WorldCoord forestCell = FindBiomeCell(overworld, BiomeId.Forest);
        var session = new GameSession(overworld, repository)
        {
            PlayerWorldPosition = forestCell
        };

        session.EnterWorldCell();
        LocalMap map = session.ActiveLocalMap!;
        Entity raptor = map.Entities.All.First(entity => entity.Kind == EntityKind.Raptor);
        raptor.Raptor!.Phase = RaptorPhase.Retreat;
        raptor.Raptor.AmbushCooldown = 4;
        raptor.Raptor.AnnouncedFenceProbe = true;
        EntityId raptorId = raptor.Id;

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "raptor");

        bool loaded = saveManager.TryLoad(
            "raptor",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.RulesHash,
            out Overworld loadedWorld,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _);

        Assert.True(loaded);
        Entity? restored = loadedRepository.GetOrGenerate(forestCell).Entities.GetById(raptorId);
        Assert.NotNull(restored);
        Assert.Equal(RaptorPhase.Retreat, restored!.Raptor!.Phase);
        Assert.Equal(4, restored.Raptor.AmbushCooldown);
        Assert.True(restored.Raptor.AnnouncedFenceProbe);

        Directory.Delete(saveDirectory, recursive: true);
    }

    private static int Manhattan(LocalCoord a, LocalCoord b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
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

    private static Entity CreateRaptor(GameSession session, LocalCoord position)
    {
        LocalMap map = session.ActiveLocalMap!;
        var raptor = new Entity
        {
            Id = new EntityId(9001),
            Kind = EntityKind.Raptor,
            WorldPosition = map.WorldPosition,
            LocalPosition = position,
            BlocksMovement = true,
            IsActive = true,
            Faction = FactionId.Wildlife,
            Actor = new ActorTurnState { Speed = 130, Energy = 100 },
            MaxHealth = 24,
            Health = 24,
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
