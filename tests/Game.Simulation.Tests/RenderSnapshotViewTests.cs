using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Items;
using Game.Simulation.Rendering;
using Game.Simulation.Scenarios;
using Game.Simulation.Session;

namespace Game.Simulation.Tests;

public sealed class RenderSnapshotViewTests
{
    [Fact]
    public void BuildRenderSnapshot_populates_player_status_inventory_and_quests()
    {
        SimulationHost host = CreateHost();
        host.ViewContent = new RenderViewContent
        {
            ItemDisplayNames = new Dictionary<int, string> { [(int)ItemId.Wood] = "Wood" },
            Quests = new Dictionary<string, (string Title, string Objective, int Target)>
            {
                [ScenarioQuestIds.Escape] = ("Escape", "Reach your escape route", 1),
                ["gather_wood"] = ("Woodcutter", "Gather 5 wood", 5)
            },
            AttributeDefinitions = [("strength", "Strength"), ("vitality", "Vitality")]
        };
        host.Session.Inventory.Add(new ItemStack(ItemId.Wood, 3));
        host.Initialize();

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.Equal(host.Session.PlayerTurnState.Energy, snapshot.PlayerStatus.Energy);
        Assert.Equal("Wood", snapshot.InventoryItems.Single().DisplayName);
        Assert.Equal(3, snapshot.InventoryItems.Single().Count);
        Assert.Contains(snapshot.QuestItems, quest => quest.Id == ScenarioQuestIds.Escape);
        Assert.Equal(2, snapshot.CharacterSheet.Attributes.Length);
    }

    [Fact]
    public void CachedSnapshot_reuses_grid_until_render_dirty()
    {
        SimulationHost host = CreateHost();
        host.Initialize();

        RenderSnapshot first = host.BuildRenderSnapshot();
        host.Session.PlayerTurnState.Energy = 42;
        host.Session.ClearRenderDirty();

        RenderSnapshot cached = host.BuildRenderSnapshot();

        Assert.Same(first.CellData, cached.CellData);
        Assert.Equal(first.PlayerStatus.Energy, cached.PlayerStatus.Energy);

        host.Session.MarkRenderDirty();
        RenderSnapshot rebuilt = host.BuildRenderSnapshot();

        Assert.Same(first.CellData, rebuilt.CellData);
        Assert.Equal(42, rebuilt.PlayerStatus.Energy);
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new OverworldGenerator().Generate(64, 64, 42UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        return new SimulationHost(overworld, session, repository);
    }
}
