using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class WalkabilityHelperTests
{
    [Theory]
    [InlineData(Direction.West, 0, 10, 10)]
    [InlineData(Direction.East, LocalMap.Width - 1, 10, 10)]
    [InlineData(Direction.North, 10, 0, 10)]
    [InlineData(Direction.South, 10, LocalMap.Height - 1, 10)]
    public void FindRoadCorridorEntry_UsesCorrectEdge(
        Direction edge,
        int expectedX,
        int expectedY,
        int localOffset)
    {
        var map = new LocalMap(new WorldCoord(5, 5));
        map.SetTerrain(expectedX, expectedY, TerrainId.Grass, TileFlags.None);

        var connections = new[] { new EdgeConnection(edge, localOffset, ConnectionType.Road, 1) };
        LocalCoord? entry = WalkabilityHelper.FindRoadCorridorEntry(map, connections);

        Assert.NotNull(entry);
        Assert.Equal(new LocalCoord(expectedX, expectedY), entry.Value);
    }
}
