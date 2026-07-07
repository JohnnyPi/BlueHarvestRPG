using Game.Generation.WorldGen;
using Game.Simulation.Coordinates;
using Game.Simulation.Pathfinding;
using Game.Simulation.World;

namespace Game.Simulation.Tests;

public class OverworldRiverTravelTests
{
    [Fact]
    public void RiverCrossing_AddsConfiguredStaminaCost()
    {
        Overworld world = new IslandWorldGenerator(TestSaveDefaults.Island).Generate(64, 64, 9201UL);
        (WorldCoord from, WorldCoord to) = FindAdjacentPassablePair(world);

        int dryCost = OverworldTravelCost.GetStepCost(world, from, to);
        world.AddEdgeConnection(from, new EdgeConnection(Direction.East, 31, ConnectionType.River, 2));
        world.AddEdgeConnection(to, new EdgeConnection(Direction.West, 31, ConnectionType.River, 2));

        int riverCost = OverworldTravelCost.GetStepCost(world, from, to);

        Assert.Equal(dryCost + OverworldTravelCost.RiverCrossingCost, riverCost);
    }

    [Fact]
    public void FindPath_WithEdgeCost_UsesParentAwarePricing()
    {
        var expensiveEdges = new HashSet<(int FromX, int FromY, int ToX, int ToY)>
        {
            (0, 1, 1, 1),
            (1, 1, 2, 1)
        };

        List<(int X, int Y)> path = GridPathfinder.FindPath(
            0,
            1,
            2,
            1,
            3,
            3,
            static (_, _) => false,
            stepCost: (fromX, fromY, toX, toY) =>
                expensiveEdges.Contains((fromX, fromY, toX, toY)) ? 100 : 1);

        Assert.NotEmpty(path);
        Assert.Equal((2, 1), path[^1]);
        Assert.DoesNotContain((1, 1), path);
    }

    private static (WorldCoord From, WorldCoord To) FindAdjacentPassablePair(Overworld world)
    {
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width - 1; x++)
            {
                var from = new WorldCoord(x, y);
                var to = new WorldCoord(x + 1, y);
                if (BiomeTraversal.IsPassable(world.GetCellValue(from).Biome) &&
                    BiomeTraversal.IsPassable(world.GetCellValue(to).Biome))
                {
                    return (from, to);
                }
            }
        }

        throw new InvalidOperationException("No adjacent passable pair found.");
    }
}
