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
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Quests;
using Game.Simulation.Session;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class SaveRoundTripTests
{
    [Fact]
    public void SaveRoundTrip_PreservesSessionAfterBorderCrossAndDamage()
    {
        string saveDirectory = Path.Combine(Path.GetTempPath(), "RougeTests", Guid.NewGuid().ToString("N"));
        var saveManager = new SaveManager(saveDirectory);
        var localGenerator = new LocalMapGenerator(TestSaveDefaults.BlueprintCatalog);
        var overworld = new IslandWorldGenerator(
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules).Generate(909UL);
        var repository = new InMemoryLocalMapRepository(overworld, localGenerator);
        var session = new GameSession(overworld, repository)
        {
            PlayerWorldPosition = new WorldCoord(10, 8)
        };

        session.EnterWorldCell();
        session.Inventory.Add(new ItemStack(ItemId.Wood, 3));
        session.QuestLog.Restore("gather_wood", QuestState.Active, 1);
        session.PressureClock.Restore(12, 10);
        session.PressureState.Restore(2, 6, pendingPredatorSpawn: true, missedEvacuation: false, hazardousTravelCell: null);

        const int edgeY = 24;
        var edgePosition = new LocalCoord(LocalMap.Width - 1, edgeY);
        session.PlayerLocalPosition = edgePosition;
        MapTransitionTestsHelper.PrepareWalkableTransition(
            overworld,
            repository,
            session.PlayerWorldPosition,
            edgePosition,
            1,
            0);

        Assert.True(session.TryMoveLocal(1, 0));

        Entity player = session.ActiveLocalMap!.Entities.GetById(EntityId.Player)!;
        var combat = new CombatResolver();
        Entity raptor = CreateRaptor(session, new LocalCoord(session.PlayerLocalPosition.X + 1, edgeY));
        Assert.True(combat.TryAttack(session, raptor, player));
        int expectedHealth = session.PlayerHealth;
        int expectedEnergy = session.PlayerTurnState.Energy;

        saveManager.Save(overworld, session, repository, TestSaveDefaults.RulesHash, "roundtrip");

        bool loaded = saveManager.TryLoad(
            "roundtrip",
            localGenerator,
            TestSaveDefaults.Island,
            TestSaveDefaults.BlueprintCatalog,
            TestSaveDefaults.BiomeRules,
            TestSaveDefaults.RulesHash,
            out Overworld loadedWorld,
            out GameSession loadedSession,
            out InMemoryLocalMapRepository loadedRepository,
            out _,
            out _);

        try
        {
            Assert.True(loaded);
            Assert.Equal(new WorldCoord(11, 8), loadedSession.PlayerWorldPosition);
            Assert.Equal(new LocalCoord(0, edgeY), loadedSession.PlayerLocalPosition);
            Assert.Equal(expectedHealth, loadedSession.PlayerHealth);
            Assert.Equal(expectedEnergy, loadedSession.PlayerTurnState.Energy);
            Assert.Equal(3, loadedSession.Inventory.Stacks.First(stack => stack.ItemId == ItemId.Wood).Count);
            Assert.Equal(QuestState.Active, loadedSession.QuestLog.Progress.First(entry => entry.QuestId == "gather_wood").State);
            Assert.Equal(12, loadedSession.PressureClock.Pressure);
            Assert.True(loadedSession.PressureState.PendingPredatorSpawn);

            Entity? loadedPlayer = loadedSession.ActiveLocalMap?.Entities.GetById(EntityId.Player);
            Assert.NotNull(loadedPlayer);
            Assert.Equal(expectedHealth, loadedPlayer.Health);
            Assert.Equal(loadedSession.PlayerLocalPosition, loadedPlayer.LocalPosition);
        }
        finally
        {
            Directory.Delete(saveDirectory, recursive: true);
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
            Actor = new Game.Simulation.Time.ActorTurnState { Speed = 130, Energy = 100 },
            MaxHealth = 24,
            Health = 24,
            Raptor = new RaptorMemory { Phase = RaptorPhase.Stalk }
        };
        map.Entities.Add(raptor);
        return raptor;
    }
}

internal static class MapTransitionTestsHelper
{
    public static void PrepareWalkableTransition(
        Overworld overworld,
        ILocalMapRepository repository,
        WorldCoord worldPosition,
        LocalCoord localPosition,
        int deltaX,
        int deltaY)
    {
        LocalMap origin = repository.GetOrGenerateSurface(worldPosition);
        origin.SetTerrain(localPosition.X, localPosition.Y, TerrainId.Grass, TileFlags.None);

        if (!MapTransitionResolver.TryResolve(
                overworld,
                worldPosition,
                localPosition,
                deltaX,
                deltaY,
                out MapTransition transition))
        {
            return;
        }

        LocalMap destination = repository.GetOrGenerateSurface(transition.DestinationWorld);
        destination.SetTerrain(
            transition.DestinationLocal.X,
            transition.DestinationLocal.Y,
            TerrainId.Grass,
            TileFlags.None);
    }
}
