using Game.Content;
using Game.Generation.Island;
using Game.Generation.LocalMaps;
using Game.Generation.Validation;
using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class EnterableCellWalkabilityTests
{
    private static readonly ulong[] Seeds = [1234UL, 4242UL, 98765UL, 555UL, 888UL];

    [Theory]
    [InlineData(IslandCellRole.VisitorCenter)]
    [InlineData(IslandCellRole.Dock)]
    [InlineData(IslandCellRole.Helipad)]
    [InlineData(IslandCellRole.Hotel)]
    [InlineData(IslandCellRole.Restaurant)]
    [InlineData(IslandCellRole.Attraction)]
    [InlineData(IslandCellRole.Maintenance)]
    [InlineData(IslandCellRole.Paddock)]
    [InlineData(IslandCellRole.Ruin)]
    [InlineData(IslandCellRole.Fortification)]
    [InlineData(IslandCellRole.Tunnel)]
    public void EnterableCells_HaveWalkableEntryAndReachability(IslandCellRole role)
    {
        var generator = new LocalMapGenerator();
        var validator = new NavigabilityValidator();

        foreach (ulong seed in Seeds)
        {
            Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(seed);
            IslandPlan plan = world.IslandPlan!;
            WorldCoord? cell = FindFirstRole(plan, role);
            if (cell is null)
            {
                continue;
            }

            LocalMap map = generator.Generate(world, MapKey.Surface(cell.Value));
            Assert.True(
                OverworldLandmarkCatalog.TryResolveEntryPoint(plan, cell.Value, map, out LocalCoord entry) ||
                role is IslandCellRole.Paddock or IslandCellRole.Tunnel,
                $"Seed {seed}: no entry for {role}.");

            if (role is IslandCellRole.Paddock or IslandCellRole.Tunnel)
            {
                entry = WalkabilityHelper.FindNearestWalkable(map, new LocalCoord(LocalMap.Width / 2, LocalMap.Height / 2));
            }

            Assert.False(map.BlocksMovement(entry), $"Seed {seed}: entry blocks movement for {role}.");
            ValidationResult result = validator.ValidateAndRepair(map, entry);
            Assert.True(
                result.ReachabilityShare >= NavigabilityValidator.MinReachabilityShare,
                $"Seed {seed}: {role} reachability {result.ReachabilityShare:P0}.");
        }
    }

    [Fact]
    public void HillsBiome_HasModerateRockDensity()
    {
        var generator = new LocalMapGenerator();
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.FullIsland).Generate(1234UL);

        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                if (world.GetCellValue(new WorldCoord(x, y)).Biome != BiomeId.Hills)
                {
                    continue;
                }

                LocalMap map = generator.Generate(world, MapKey.Surface(new WorldCoord(x, y)));
                int rockCount = map.Terrain.Count(terrain => terrain == TerrainId.Rock);
                float rockShare = rockCount / (float)(LocalMap.Width * LocalMap.Height);
                Assert.True(rockShare < 0.50f, $"Hills cell ({x},{y}) had {rockShare:P0} rock.");
            }
        }
    }

    [Fact]
    public void FacilityCells_UsePlainsOrBeachAfterFinalize()
    {
        foreach (ulong seed in Seeds)
        {
            IslandPlan plan = new IslandPlanner(TestSaveDefaults.Island).Generate(64, 64, seed);
            IslandCellRole[] facilityRoles =
            [
                IslandCellRole.VisitorCenter,
                IslandCellRole.Hotel,
                IslandCellRole.Restaurant,
                IslandCellRole.Attraction,
                IslandCellRole.Helipad,
                IslandCellRole.Maintenance,
                IslandCellRole.Road
            ];

            foreach (IslandCellRole role in facilityRoles)
            {
                WorldCoord? cell = FindFirstRole(plan, role);
                if (cell is null)
                {
                    continue;
                }

                BiomeId biome = plan.GetCell(cell.Value).Biome;
                Assert.True(
                    biome is BiomeId.Plains or BiomeId.Beach,
                    $"Seed {seed}: {role} on {biome}.");
            }

            WorldCoord? dock = FindFirstRole(plan, IslandCellRole.Dock);
            if (dock is not null)
            {
                BiomeId dockBiome = plan.GetCell(dock.Value).Biome;
                Assert.True(
                    dockBiome is BiomeId.Beach or BiomeId.Plains,
                    $"Seed {seed}: dock on {dockBiome}.");
            }
        }
    }

    [Fact]
    public void HotelDoor_RemainsDoorTerrainAfterGeneration()
    {
        var bundle = new ContentLoader().LoadAll();
        TestSaveDefaults.ApplyFastOceanFrameForTests(bundle.Island);
        var catalog = bundle.CreateBlueprintCatalog();
        var generator = new LocalMapGenerator(catalog);
        Overworld world = new IslandWorldGenerator(bundle.Island, catalog).Generate(9012UL);
        StructurePlacement hotel = world.IslandPlan!.Structures.First(s => s.Type == StructureType.Hotel);
        var blueprint = catalog.ResolveById(hotel.BlueprintId);
        WorldCoord doorCell = CoordinateMath.FromGlobalTile(
            new GlobalTileCoord(hotel.GlobalOriginX + blueprint.DoorX, hotel.GlobalOriginY + blueprint.DoorY)).World;
        LocalMap map = generator.Generate(world, MapKey.Surface(doorCell));
        LocalCoord door = StructurePlacementQueries.ToLocalCoord(doorCell, hotel, blueprint.DoorX, blueprint.DoorY);

        Assert.Equal(TerrainId.Door, map.Terrain[map.GetIndex(door.X, door.Y)]);
    }

    private static WorldCoord? FindFirstRole(IslandPlan plan, IslandCellRole role)
    {
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                if (role == IslandCellRole.VisitorCenter &&
                    plan.VisitorCenterCell.X == x &&
                    plan.VisitorCenterCell.Y == y)
                {
                    return new WorldCoord(x, y);
                }

                if (role != IslandCellRole.VisitorCenter && plan.GetCell(x, y).Role.HasFlag(role))
                {
                    return new WorldCoord(x, y);
                }
            }
        }

        return null;
    }
}
