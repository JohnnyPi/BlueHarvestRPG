using Game.Content;
using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.Items;
using Game.Simulation.LocalMaps;
using Game.Simulation.Session;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class StructureLevelsTests
{
    [Fact]
    public void StructureFinalize_AssignsBlueprintMetadata()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(1234UL);
        StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);

        Assert.True(hotel.InstanceId > 0);
        Assert.Equal("hotel", hotel.BlueprintId);
        Assert.Equal(3, hotel.FloorCount);
    }

    [Fact]
    public void HotelInterior_GeneratesMultipleFloorsWithAlignedStairs()
    {
        var catalog = new ContentLoader().LoadAll().CreateBlueprintCatalog();
        var generator = new LocalMapGenerator(catalog);
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island, catalog).Generate(5678UL);
        StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);
        var blueprint = catalog.ResolveById(hotel.BlueprintId);
        WorldCoord cell = ResolveStructureCell(hotel, blueprint.StairX, blueprint.StairY);

        LocalMap ground = generator.Generate(world, new MapKey(cell, hotel.InstanceId, 0));
        LocalMap upper = generator.Generate(world, new MapKey(cell, hotel.InstanceId, 1));
        LocalMap top = generator.Generate(world, new MapKey(cell, hotel.InstanceId, 2));

        LocalCoord stair = StructurePlacementQueries.ToLocalCoord(cell, hotel, blueprint.StairX, blueprint.StairY);
        LocalCoord rope = StructurePlacementQueries.ToLocalCoord(
            ResolveStructureCell(hotel, blueprint.RopeExitX!.Value, blueprint.RopeExitY!.Value),
            hotel,
            blueprint.RopeExitX!.Value,
            blueprint.RopeExitY!.Value);

        Assert.Equal(TerrainId.StairsUp, ground.Terrain[ground.GetIndex(stair.X, stair.Y)]);
        Assert.Equal(TerrainId.StairsUp, upper.Terrain[upper.GetIndex(stair.X, stair.Y)]);
        Assert.Equal(TerrainId.Window, top.Terrain[top.GetIndex(rope.X, rope.Y)]);
    }

    [Fact]
    public void StructureTransition_EnterExitAndStairs()
    {
        var bundle = new ContentLoader().LoadAll();
        TestSaveDefaults.ApplyFastOceanFrameForTests(bundle.Island);
        var catalog = bundle.CreateBlueprintCatalog();
        var generator = new LocalMapGenerator(catalog);
        Overworld world = new IslandWorldGenerator(bundle.Island, catalog).Generate(9012UL);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, generator);
        var session = new GameSession(world, repository);

        StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);
        var blueprint = catalog.ResolveById(hotel.BlueprintId);
        WorldCoord doorCell = ResolveStructureCell(hotel, blueprint.DoorX, blueprint.DoorY);
        WorldCoord stairCell = ResolveStructureCell(hotel, blueprint.StairX, blueprint.StairY);
        LocalCoord door = StructurePlacementQueries.ToLocalCoord(doorCell, hotel, blueprint.DoorX, blueprint.DoorY);
        LocalCoord stair = StructurePlacementQueries.ToLocalCoord(stairCell, hotel, blueprint.StairX, blueprint.StairY);

        session.ViewMode = GameViewMode.LocalMap;
        session.PlayerWorldPosition = doorCell;
        session.ActiveLocalMap = repository.GetOrGenerateSurface(doorCell);
        session.PlayerLocalPosition = door;

        Assert.True(session.TryUseTileTransition(door.X, door.Y, catalog));
        Assert.True(session.IsInStructureInterior);
        Assert.Equal(0, session.ActiveLocalMap!.FloorIndex);

        session.PlayerLocalPosition = stair;
        Assert.True(session.TryUseTileTransition(stair.X, stair.Y, catalog, TileTransitionKind.StairsUp));
        Assert.Equal(1, session.ActiveLocalMap!.FloorIndex);

        session.PlayerLocalPosition = stair;
        Assert.True(session.TryUseTileTransition(stair.X, stair.Y, catalog, TileTransitionKind.StairsUp));
        Assert.Equal(2, session.ActiveLocalMap!.FloorIndex);

        LocalCoord exit = StructurePlacementQueries.ToLocalCoord(doorCell, hotel, blueprint.DoorX, blueprint.DoorY);
        session.PlayerLocalPosition = exit;
        Assert.False(session.TryUseTileTransition(exit.X, exit.Y, catalog));
        session.PlayerLocalPosition = stair;
        Assert.True(session.TryUseTileTransition(stair.X, stair.Y, catalog, TileTransitionKind.StairsDown));
        Assert.Equal(1, session.ActiveLocalMap!.FloorIndex);

        session.PlayerLocalPosition = stair;
        Assert.True(session.TryUseTileTransition(stair.X, stair.Y, catalog, TileTransitionKind.StairsDown));
        exit = StructurePlacementQueries.ToLocalCoord(doorCell, hotel, blueprint.DoorX, blueprint.DoorY);
        session.PlayerLocalPosition = exit;
        Assert.True(session.TryUseTileTransition(exit.X, exit.Y, catalog));
        Assert.False(session.IsInStructureInterior);
    }

    [Fact]
    public void RopeDescent_RequiresRopeItem()
    {
        var bundle = new ContentLoader().LoadAll();
        TestSaveDefaults.ApplyFastOceanFrameForTests(bundle.Island);
        var catalog = bundle.CreateBlueprintCatalog();
        var generator = new LocalMapGenerator(catalog);
        Overworld world = new IslandWorldGenerator(bundle.Island, catalog).Generate(3456UL);
        var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, generator);
        var session = new GameSession(world, repository);

        StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);
        var blueprint = catalog.ResolveById(hotel.BlueprintId);
        WorldCoord ropeCell = ResolveStructureCell(hotel, blueprint.RopeExitX!.Value, blueprint.RopeExitY!.Value);
        LocalCoord ropeTile = StructurePlacementQueries.ToLocalCoord(
            ropeCell,
            hotel,
            blueprint.RopeExitX!.Value,
            blueprint.RopeExitY!.Value);

        session.ViewMode = GameViewMode.LocalMap;
        session.PlayerWorldPosition = ropeCell;
        session.ActiveLocalMap = generator.Generate(world, new MapKey(ropeCell, hotel.InstanceId, 2));
        session.PlayerLocalPosition = ropeTile;

        Assert.False(session.TryUseTileTransition(ropeTile.X, ropeTile.Y, catalog));
        session.Inventory.Add(new ItemStack(ItemId.Rope, 1));
        Assert.True(session.TryUseTileTransition(ropeTile.X, ropeTile.Y, catalog));
        Assert.False(session.IsInStructureInterior);
    }

    [Fact]
    public void StructureFloorSave_RoundTripsThroughPersistence()
    {
        string saveDir = Path.Combine(Path.GetTempPath(), "RougeStructureTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveDir);

        try
        {
            var bundle = new ContentLoader().LoadAll();
            TestSaveDefaults.ApplyFastOceanFrameForTests(bundle.Island);
            var catalog = bundle.CreateBlueprintCatalog();
            var generator = new LocalMapGenerator(catalog);
            Overworld world = new IslandWorldGenerator(bundle.Island, catalog).Generate(7890UL);
            var repository = new Game.Persistence.Repositories.InMemoryLocalMapRepository(world, generator);
            var session = new GameSession(world, repository);

            StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);
            var blueprint = catalog.ResolveById(hotel.BlueprintId);
            WorldCoord cell = ResolveStructureCell(hotel, blueprint.StairX, blueprint.StairY);

            session.ViewMode = GameViewMode.LocalMap;
            session.PlayerWorldPosition = cell;
            session.ActiveLocalMap = generator.Generate(world, new MapKey(cell, hotel.InstanceId, 1));
            session.PlayerLocalPosition = new LocalCoord(10, 10);
            session.EnsurePlayerEntity();
            repository.Store(session.ActiveLocalMap);

            var saveManager = new Game.Persistence.Saves.SaveManager(saveDir);
            uint rulesHash = BiomeRulesHash.Compute(bundle.BiomeRules);
            saveManager.Save(world, session, repository, rulesHash);

            Assert.True(saveManager.TryLoad(
                "autosave",
                generator,
                bundle.Island,
                rulesHash,
                out Overworld loadedWorld,
                out GameSession loadedSession,
                out var loadedRepository,
                out string? failure),
                failure);

            Assert.True(loadedSession.IsInStructureInterior);
            Assert.Equal(1, loadedSession.ActiveLocalMap!.FloorIndex);
            Assert.Equal(hotel.InstanceId, loadedSession.ActiveLocalMap.StructureInstanceId);
        }
        finally
        {
            if (Directory.Exists(saveDir))
            {
                Directory.Delete(saveDir, recursive: true);
            }
        }
    }

    private static WorldCoord ResolveStructureCell(StructurePlacement structure, int withinX, int withinY)
    {
        return CoordinateMath.FromGlobalTile(
            new GlobalTileCoord(structure.GlobalOriginX + withinX, structure.GlobalOriginY + withinY)).World;
    }
}
