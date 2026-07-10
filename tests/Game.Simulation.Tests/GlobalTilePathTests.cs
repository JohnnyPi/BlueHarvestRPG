using Game.Generation.Passes;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Tests;

public class GlobalTilePathTests
{
    [Fact]
    public void AddPathWithBorderRuns_CreatesContiguousHorizontalWidth()
    {
        var tiles = new HashSet<(int GlobalX, int GlobalY)>();

        GlobalTilePathUtility.AddPathWithBorderRuns(
            tiles,
            [new WorldCoord(0, 0), new WorldCoord(1, 0)],
            width: 3);

        Assert.True(GlobalTileConnectivityValidator.IsConnected(tiles));
        for (int x = LocalMap.Width / 2; x <= LocalMap.Width + LocalMap.Width / 2; x++)
        {
            Assert.Contains((x, LocalMap.Height / 2 - 1), tiles);
            Assert.Contains((x, LocalMap.Height / 2), tiles);
            Assert.Contains((x, LocalMap.Height / 2 + 1), tiles);
        }
    }

    [Fact]
    public void AddPathWithBorderRuns_ExpandsVerticalWidthHorizontally()
    {
        var tiles = new HashSet<(int GlobalX, int GlobalY)>();

        GlobalTilePathUtility.AddPathWithBorderRuns(
            tiles,
            [new WorldCoord(0, 0), new WorldCoord(0, 1)],
            width: 3);

        int centerX = LocalMap.Width / 2;
        int centerY = LocalMap.Height;
        Assert.Contains((centerX - 1, centerY), tiles);
        Assert.Contains((centerX, centerY), tiles);
        Assert.Contains((centerX + 1, centerY), tiles);
        Assert.DoesNotContain((centerX + 2, centerY), tiles);
        Assert.True(GlobalTileConnectivityValidator.IsConnected(tiles));
    }

    [Fact]
    public void ConnectivityValidator_DetectsTileLevelGap()
    {
        var tiles = new HashSet<(int GlobalX, int GlobalY)> { (4, 7), (5, 7), (7, 7) };

        Assert.False(GlobalTileConnectivityValidator.IsConnected(tiles));
        Assert.Equal(2, GlobalTileConnectivityValidator.CountConnectedTiles(tiles));
    }

    [Fact]
    public void AuthoritativeGraphs_PreventIndependentBoundaryCorridors()
    {
        var plan = new IslandPlan(2, 1, 1UL);
        plan.RoadGraph.GlobalPathTiles.Add((10, 10));
        plan.RiverGraph.GlobalRiverTiles.Add((11, 11));
        var map = new LocalMap(MapKey.Surface(new WorldCoord(0, 0)));
        var context = CreateContext(
            plan,
            [new EdgeConnection(Direction.East, 20, ConnectionType.Road, 2),
             new EdgeConnection(Direction.East, 24, ConnectionType.River, 2)]);

        new BoundaryConnectionPass().Execute(map, context);

        Assert.NotEqual(TerrainId.Road, map.Terrain[map.GetIndex(LocalMap.Width - 1, 20)]);
        Assert.NotEqual(TerrainId.ShallowFord, map.Terrain[map.GetIndex(LocalMap.Width - 1, 24)]);
    }

    [Fact]
    public void RoadRiverCrossing_RemainsFordInEitherPassOrder()
    {
        var plan = new IslandPlan(1, 1, 1UL);
        plan.RoadGraph.GlobalPathTiles.Add((10, 10));
        plan.RiverGraph.GlobalRiverTiles.Add((10, 10));
        LocalGenerationContext context = CreateContext(plan, []);

        var riverThenRoad = new LocalMap(MapKey.Surface(new WorldCoord(0, 0)));
        new RiverStampPass().Execute(riverThenRoad, context);
        new FacilityRoadStampPass().Execute(riverThenRoad, context);

        var roadThenRiver = new LocalMap(MapKey.Surface(new WorldCoord(0, 0)));
        new FacilityRoadStampPass().Execute(roadThenRiver, context);
        new RiverStampPass().Execute(roadThenRiver, context);

        Assert.Equal(TerrainId.ShallowFord, riverThenRoad.Terrain[riverThenRoad.GetIndex(10, 10)]);
        Assert.Equal(TerrainId.ShallowFord, roadThenRiver.Terrain[roadThenRiver.GetIndex(10, 10)]);
    }

    private static LocalGenerationContext CreateContext(
        IslandPlan plan,
        IReadOnlyList<EdgeConnection> connections)
    {
        return new LocalGenerationContext
        {
            Seed = 1UL,
            WorldCoordinate = new WorldCoord(0, 0),
            WorldCell = default,
            Connections = connections,
            IslandPlan = plan,
            BlueprintCatalog = StructureBlueprintCatalogDefaults.Create()
        };
    }
}
