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

public class OverworldVisualizationTests
{
    [Fact]
    public void RevealAll_SetsEveryExploredTile()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(123UL);
        Assert.Contains(overworld.Explored, explored => !explored);

        OverworldExploration.RevealAll(overworld);

        Assert.All(overworld.Explored, Assert.True);
    }

    [Fact]
    public void RevealEntireOverworld_EnablesDebugFullBrightnessSnapshot()
    {
        SimulationHost host = CreateHost();
        host.Session.RevealEntireOverworld();

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.True(host.Session.DebugRevealAll);
        Assert.True(snapshot.DebugFullBrightness);
        Assert.All(snapshot.ExploredTiles!, Assert.True);
        Assert.All(snapshot.VisibleTiles!, Assert.True);
    }

    [Fact]
    public void BuildRenderSnapshot_IncludesRoadAndLavaMasksForIslandWorld()
    {
        SimulationHost host = CreateHost();
        OverworldExploration.RevealAll(host.Overworld);

        RenderSnapshot snapshot = host.BuildRenderSnapshot();

        Assert.NotNull(snapshot.RoadEdgeMask);
        Assert.NotNull(snapshot.RoadCells);
        Assert.NotNull(snapshot.LavaCells);
        Assert.Contains(snapshot.RoadEdgeMask, mask => mask != 0);
        Assert.Contains(snapshot.RoadCells, isRoad => isRoad);
        Assert.Contains(snapshot.LavaCells, isLava => isLava);
    }

    [Fact]
    public void RoadEdgeMask_UsesLowNibbleOfConnectionFlags()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        SimulationHost host = CreateHost(overworld);

        WorldCoord? roadCell = FindFirstRoadConnectionCell(overworld);
        Assert.NotNull(roadCell);

        ref WorldCell cell = ref overworld.GetCell(roadCell.Value);
        byte expected = (byte)((ushort)cell.ConnectionFlags & 0x0F);
        Assert.NotEqual(0, expected);

        RenderSnapshot snapshot = host.BuildRenderSnapshot();
        int index = overworld.GetIndex(roadCell.Value);

        Assert.NotNull(snapshot.RoadEdgeMask);
        Assert.Equal(expected, snapshot.RoadEdgeMask[index]);
    }

    [Fact]
    public void CollectExploredLandmarks_ReturnsOneEntryPerStructure()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        OverworldExploration.RevealAll(overworld);

        IReadOnlyList<OverworldLandmark> landmarks =
            OverworldLandmarkCatalog.CollectExploredLandmarks(overworld, scenario: null);

        int structureCount = overworld.IslandPlan!.Structures.Count;
        int structureLandmarks = landmarks.Count(landmark => landmark.Kind == OverworldLandmarkKind.Structure);

        Assert.Equal(structureCount, structureLandmarks);
        Assert.DoesNotContain(
            landmarks,
            landmark => landmark.Kind == OverworldLandmarkKind.Structure && landmark.Width <= 0);
    }

    [Fact]
    public void CollectExploredLandmarks_StructureUsesFootprintCoordinates()
    {
        Overworld overworld = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(4242UL);
        StructurePlacement visitor = overworld.IslandPlan!.Structures
            .First(structure => structure.Type == StructureType.VisitorCenter);
        OverworldExploration.RevealAround(overworld, overworld.IslandPlan.VisitorCenterCell, 0);

        OverworldLandmark landmark = OverworldLandmarkCatalog
            .CollectExploredLandmarks(overworld, scenario: null)
            .First(entry => entry.Name == "Visitor Center");

        Assert.Equal(visitor.GlobalOriginX, landmark.GlobalOriginX);
        Assert.Equal(visitor.GlobalOriginY, landmark.GlobalOriginY);
        Assert.Equal(visitor.Width, landmark.Width);
        Assert.Equal(visitor.Height, landmark.Height);
    }

    private static SimulationHost CreateHost(Overworld? overworld = null)
    {
        overworld ??= new IslandWorldGenerator(TestSaveDefaults.Island).Generate(777UL);
        var repository = new InMemoryLocalMapRepository(overworld, new LocalMapGenerator());
        var session = new GameSession(overworld, repository);
        var host = new SimulationHost(overworld, session, repository) { IsNewGame = true };
        host.Initialize();
        return host;
    }

    private static WorldCoord? FindFirstRoadConnectionCell(Overworld overworld)
    {
        for (int y = 0; y < overworld.Height; y++)
        {
            for (int x = 0; x < overworld.Width; x++)
            {
                ConnectionFlags flags = overworld.GetCellValue(new WorldCoord(x, y)).ConnectionFlags;
                if (((ushort)flags & 0x0F) != 0)
                {
                    return new WorldCoord(x, y);
                }
            }
        }

        return null;
    }
}
