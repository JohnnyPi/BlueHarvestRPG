using Game.Generation.LocalMaps;
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
    public void CollectExploredLandmarks_IncludesMajorSiteTypes()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        OverworldExploration.InitializeTouristMap(overworld);

        foreach (WorldCoord coord in FindLandmarkCells(overworld.IslandPlan!))
        {
            OverworldExploration.RevealAround(overworld, coord, 0);
        }

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
    }

    [Fact]
    public void EnterWorldCell_AtStructure_UsesDoorEntryPoint()
    {
        SimulationHost host = CreateHost();
        IslandPlan plan = host.Overworld.IslandPlan!;
        WorldCoord visitor = plan.VisitorCenterCell;
        var localGenerator = new LocalMapGenerator();
        LocalMap preview = localGenerator.Generate(host.Overworld, MapKey.Surface(visitor));

        Assert.True(OverworldLandmarkCatalog.TryResolveEntryPoint(plan, visitor, preview, out LocalCoord entry));
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

    private static IEnumerable<WorldCoord> FindLandmarkCells(IslandPlan plan)
    {
        IslandCellRole[] roles =
        [
            IslandCellRole.VisitorCenter,
            IslandCellRole.Dock,
            IslandCellRole.Helipad,
            IslandCellRole.Maintenance,
            IslandCellRole.Restaurant
        ];

        foreach (IslandCellRole role in roles)
        {
            WorldCoord? coord = FindFirstRole(plan, role);
            if (coord is not null)
            {
                yield return coord.Value;
            }
        }
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
