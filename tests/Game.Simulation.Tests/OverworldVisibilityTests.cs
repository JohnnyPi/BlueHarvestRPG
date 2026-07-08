using Game.Generation.LocalMaps;
using Game.Generation.WorldGen;
using Game.Persistence.Repositories;
using Game.Simulation;
using Game.Simulation.Coordinates;
using Game.Simulation.Rendering;
using Game.Simulation.Session;
using Game.Simulation.Visibility;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class OverworldVisibilityTests
{
    [Fact]
    public void ComputeVisible_OnlyMarksTilesWithinRevealRadius()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(1234UL);
        var visible = new bool[overworld.Width * overworld.Height];
        WorldCoord center = overworld.IslandPlan!.VisitorCenterCell;

        OverworldExploration.ComputeVisible(overworld, center, visible);

        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                int dx = x - center.X;
                int dy = y - center.Y;
                bool inRadius = dx * dx + dy * dy <= OverworldExploration.RevealRadius * OverworldExploration.RevealRadius;
                int index = overworld.GetIndex(new WorldCoord(x, y));
                Assert.Equal(inRadius && overworld.Contains(new WorldCoord(x, y)), visible[index]);
            }
        }
    }

    [Fact]
    public void BuildOverworldSnapshot_ExploredButNotVisibleTilesAreOutsideCurrentFov()
    {
        SimulationHost host = CreateHost();
        WorldCoord player = host.Session.PlayerWorldPosition;
        WorldCoord farCell = FindFarUnexploredLandCell(host.Overworld, player);
        int farIndex = host.Overworld.GetIndex(farCell);

        host.Overworld.Explored[farIndex] = true;
        host.Session.UpdateVisibility();

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.NotNull(snapshot.VisibleTiles);
        Assert.NotNull(snapshot.ExploredTiles);
        Assert.True(snapshot.ExploredTiles[farIndex]);
        Assert.False(snapshot.VisibleTiles[farIndex]);
    }

    [Fact]
    public void InitializeTouristMap_DoesNotPreExploreEntireCoastline()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(5678UL);
        IslandPlan plan = overworld.IslandPlan!;
        int coastCells = 0;
        int exploredCoastCells = 0;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.GetCell(x, y).IsCoast)
                {
                    continue;
                }

                coastCells++;
                if (overworld.Explored[overworld.GetIndex(new WorldCoord(x, y))])
                {
                    exploredCoastCells++;
                }
            }
        }

        Assert.True(coastCells > OverworldExploration.RevealRadius);
        OverworldExploration.InitializeTouristMap(overworld);

        exploredCoastCells = 0;
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.GetCell(x, y).IsCoast)
                {
                    continue;
                }

                if (overworld.Explored[overworld.GetIndex(new WorldCoord(x, y))])
                {
                    exploredCoastCells++;
                }
            }
        }

        Assert.True(exploredCoastCells < coastCells);
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

    private static WorldCoord FindFarUnexploredLandCell(Overworld overworld, WorldCoord from)
    {
        IslandPlan plan = overworld.IslandPlan!;
        WorldCoord best = from;
        int bestDistance = 0;

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                int dx = x - from.X;
                int dy = y - from.Y;
                int distance = dx * dx + dy * dy;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = new WorldCoord(x, y);
                }
            }
        }

        return best;
    }
}
