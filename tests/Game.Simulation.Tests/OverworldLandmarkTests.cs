using Game.Generation.LocalMaps;
using Game.Generation.Island;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class OverworldLandmarkTests
{
    [Fact]
    public void FootprintValidation_RejectsBoundsOceanAndOverlap()
    {
        var plan = new IslandPlan(4, 4, 1UL);
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                plan.GetCell(x, y).IsLand = true;
            }
        }

        Assert.False(IslandPlacementHelper.CanPlaceFootprint(plan, -1, 0, 96, 80));
        Assert.True(IslandPlacementHelper.CanPlaceFootprint(plan, 32, 32, 96, 80));

        plan.GetCell(1, 1).IsLand = false;
        Assert.False(IslandPlacementHelper.CanPlaceFootprint(plan, 32, 32, 96, 80));
        plan.GetCell(1, 1).IsLand = true;

        plan.Structures.Add(StructurePlacement.CreatePending(StructureType.Hotel, 80, 80, 24, 18));
        Assert.False(IslandPlacementHelper.CanPlaceFootprint(plan, 32, 32, 96, 80));
    }

    [Fact]
    public void GeneratedMajorStructures_UseValidNonOverlappingMultiCellFootprints()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        IslandPlan plan = overworld.IslandPlan!;
        StructurePlacement visitor =
            plan.Structures.Single(structure => structure.Type == StructureType.VisitorCenter);

        Assert.True(visitor.Width > LocalMap.Width || visitor.Height > LocalMap.Height);
        AssertFootprintIsLand(plan, visitor);

        for (int i = 0; i < plan.Structures.Count; i++)
        {
            for (int j = i + 1; j < plan.Structures.Count; j++)
            {
                Assert.False(Overlaps(plan.Structures[i], plan.Structures[j]));
            }
        }
    }

    [Fact]
    public void MultiCellStructure_StampsEveryOverlappedSurfaceCellAndRenderData()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        OverworldExploration.RevealAll(overworld);
        IslandPlan plan = overworld.IslandPlan!;
        StructurePlacement visitor =
            plan.Structures.Single(structure => structure.Type == StructureType.VisitorCenter);
        var generator = new LocalMapGenerator();

        int minCellX = visitor.GlobalOriginX / LocalMap.Width;
        int minCellY = visitor.GlobalOriginY / LocalMap.Height;
        int maxCellX = (visitor.GlobalOriginX + visitor.Width - 1) / LocalMap.Width;
        int maxCellY = (visitor.GlobalOriginY + visitor.Height - 1) / LocalMap.Height;
        Assert.True(maxCellX > minCellX || maxCellY > minCellY);

        for (int y = minCellY; y <= maxCellY; y++)
        {
            for (int x = minCellX; x <= maxCellX; x++)
            {
                LocalMap map = generator.Generate(overworld, MapKey.Surface(new WorldCoord(x, y)));
                Assert.Contains(
                    map.Terrain,
                    terrain => terrain is TerrainId.Wall or TerrainId.InteriorWall or TerrainId.Floor or TerrainId.Door);
            }
        }

        IReadOnlyList<OverworldLandmark> landmarks =
            OverworldLandmarkCatalog.CollectExploredLandmarks(overworld, scenario: null);
        OverworldLandmark landmark = landmarks.Single(item => item.Name == "Visitor Center");
        Assert.Equal(visitor.GlobalOriginX, landmark.GlobalOriginX);
        Assert.Equal(visitor.GlobalOriginY, landmark.GlobalOriginY);
        Assert.Equal(visitor.Width, landmark.Width);
        Assert.Equal(visitor.Height, landmark.Height);
    }

    [Fact]
    public void MultiCellStructure_SurfaceRoadReachesDoor()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        StructurePlacement visitor =
            overworld.IslandPlan!.Structures.Single(structure => structure.Type == StructureType.VisitorCenter);
        StructureBlueprintDefinition blueprint =
            StructureBlueprintCatalogDefaults.Create().ResolveById(visitor.BlueprintId);
        WorldCoord doorCell = StructurePlacementQueries.DoorCell(visitor, blueprint);
        LocalCoord door = StructurePlacementQueries.SurfaceDoorLocal(visitor, blueprint);
        LocalMap map = new LocalMapGenerator().Generate(overworld, MapKey.Surface(doorCell));
        Assert.Contains(TerrainId.Road, map.Terrain);
        LocalCoord road = FindNearest(map, door, TerrainId.Road);

        Assert.Equal(TerrainId.Door, map.Terrain[map.GetIndex(door.X, door.Y)]);
        Assert.True(IsReachable(map, road, door));
    }

    [Fact]
    public void CollectExploredLandmarks_IncludesMajorSiteTypes()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        OverworldExploration.RevealAll(overworld);

        IReadOnlyList<OverworldLandmark> landmarks =
            OverworldLandmarkCatalog.CollectExploredLandmarks(overworld, scenario: null);

        Assert.Contains(landmarks, landmark => landmark.Name == "Visitor Center");
        Assert.Contains(landmarks, landmark => landmark.Name.StartsWith("Dock", StringComparison.Ordinal));
        Assert.Contains(landmarks, landmark => landmark.Name == "Helipad");
        Assert.Contains(landmarks, landmark => landmark.Name == "Maintenance compound");
        Assert.Contains(landmarks, landmark => landmark.Name == "Restaurant");
    }

    [Fact]
    public void CollectExploredLandmarks_OmitsUnexploredSites()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(99UL);
        IslandPlan plan = overworld.IslandPlan!;
        WorldCoord? dockCoord = FindFirstRole(plan, IslandCellRole.Dock);
        Assert.NotNull(dockCoord);

        Assert.False(string.IsNullOrEmpty(OverworldLandmarkCatalog.GetName(plan, dockCoord.Value.X, dockCoord.Value.Y)));

        IReadOnlyList<OverworldLandmark> landmarks =
            OverworldLandmarkCatalog.CollectExploredLandmarks(overworld, scenario: null);

        Assert.DoesNotContain(landmarks, landmark => landmark.X == dockCoord.Value.X && landmark.Y == dockCoord.Value.Y);
    }

    [Fact]
    public void BuildRenderSnapshot_IncludesExploredLandmarksOnOverworld()
    {
        SimulationHost host = CreateHost();
        Overworld overworld = host.Overworld;
        WorldCoord? dockCoord = FindFirstRole(overworld.IslandPlan!, IslandCellRole.Dock);
        Assert.NotNull(dockCoord);
        OverworldExploration.RevealAround(overworld, dockCoord.Value, 0);

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.Equal(GameViewMode.Overworld, snapshot.ViewMode);
        Assert.NotNull(snapshot.OverworldLandmarks);
        Assert.Contains(snapshot.OverworldLandmarks, landmark => landmark.Name.StartsWith("Dock", StringComparison.Ordinal));
        Assert.True(snapshot.OverworldLandmarks!.First(landmark => landmark.Name.StartsWith("Dock", StringComparison.Ordinal)).FootprintWidth > 0);
    }

    [Fact]
    public void EnterWorldCell_AtStructure_UsesDoorEntryPoint()
    {
        SimulationHost host = CreateHost();
        IslandPlan plan = host.Overworld.IslandPlan!;
        StructurePlacement visitor = plan.Structures.First(structure => structure.Type == StructureType.VisitorCenter);
        var blueprint = StructureBlueprintCatalogDefaults.Create().Resolve(visitor.Type);
        WorldCoord doorCell = StructurePlacementQueries.DoorCell(visitor, blueprint);
        var localGenerator = new LocalMapGenerator();
        LocalMap preview = localGenerator.Generate(host.Overworld, MapKey.Surface(doorCell));

        Assert.True(OverworldLandmarkCatalog.TryResolveEntryPoint(plan, doorCell, preview, out LocalCoord entry));
        Assert.True(preview.Contains(entry));
        Assert.False(preview.BlocksMovement(entry));
    }

    private static SimulationHost CreateHost()
    {
        var overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(777UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
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

    private static void AssertFootprintIsLand(IslandPlan plan, StructurePlacement structure)
    {
        int minCellX = structure.GlobalOriginX / LocalMap.Width;
        int minCellY = structure.GlobalOriginY / LocalMap.Height;
        int maxCellX = (structure.GlobalOriginX + structure.Width - 1) / LocalMap.Width;
        int maxCellY = (structure.GlobalOriginY + structure.Height - 1) / LocalMap.Height;
        for (int y = minCellY; y <= maxCellY; y++)
        {
            for (int x = minCellX; x <= maxCellX; x++)
            {
                Assert.True(plan.Contains(x, y) && plan.IsLand(x, y));
            }
        }
    }

    private static bool Overlaps(StructurePlacement left, StructurePlacement right) =>
        left.GlobalOriginX < right.GlobalOriginX + right.Width &&
        left.GlobalOriginX + left.Width > right.GlobalOriginX &&
        left.GlobalOriginY < right.GlobalOriginY + right.Height &&
        left.GlobalOriginY + left.Height > right.GlobalOriginY;

    private static LocalCoord FindNearest(LocalMap map, LocalCoord target, TerrainId terrain)
    {
        return Enumerable.Range(0, LocalMap.Width * LocalMap.Height)
            .Where(index => map.Terrain[index] == terrain)
            .Select(index => new LocalCoord(index % LocalMap.Width, index / LocalMap.Width))
            .MinBy(coord => Math.Abs(coord.X - target.X) + Math.Abs(coord.Y - target.Y));
    }

    private static bool IsReachable(LocalMap map, LocalCoord start, LocalCoord target)
    {
        var visited = new HashSet<LocalCoord> { start };
        var queue = new Queue<LocalCoord>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            LocalCoord current = queue.Dequeue();
            if (current == target)
            {
                return true;
            }

            foreach ((int dx, int dy) in new[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                var next = new LocalCoord(current.X + dx, current.Y + dy);
                if (map.Contains(next) && !map.BlocksMovement(next) && visited.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }
}
