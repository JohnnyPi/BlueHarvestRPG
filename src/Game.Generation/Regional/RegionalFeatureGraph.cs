using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Regional;

public static class RegionalFeatureGraph
{
    private const ulong RoadSeedSalt = 0x524F41445F475241UL;
    private const ulong RiverSeedSalt = 0x524956455F475241UL;
    private const int RoadWidth = 2;

    public static void ApplyRoads(Overworld world)
    {
        var random = new DeterministicRandom(world.Seed ^ RoadSeedSalt);

        int horizontalRoadCount = 2 + random.NextInt(3);
        for (int i = 0; i < horizontalRoadCount; i++)
        {
            int worldY = random.NextInt(world.Height);
            int localOffset = 28 + random.NextInt(LocalMap.Height - RoadWidth - 28);
            AddHorizontalRoad(world, worldY, localOffset, RoadWidth);
        }

        int verticalRoadCount = 2 + random.NextInt(3);
        for (int i = 0; i < verticalRoadCount; i++)
        {
            int worldX = random.NextInt(world.Width);
            int localOffset = 28 + random.NextInt(LocalMap.Width - RoadWidth - 28);
            AddVerticalRoad(world, worldX, localOffset, RoadWidth);
        }
    }

    public static void ApplyRivers(Overworld world, IslandPlan plan, IslandDefinition config)
    {
        if (config.RiverCount <= 0)
        {
            return;
        }

        IReadOnlyList<(int X, int Y)> sources = CollectRiverSources(plan, config);
        var tracedCells = new HashSet<(int X, int Y)>();
        int riversPlaced = 0;

        foreach ((int sourceX, int sourceY) in sources)
        {
            if (riversPlaced >= config.RiverCount)
            {
                break;
            }

            int connectionsBefore = CountRiverConnections(world);
            TraceRiver(world, plan, sourceX, sourceY, config, tracedCells);
            if (CountRiverConnections(world) > connectionsBefore)
            {
                riversPlaced++;
            }
        }
    }

    private static int CountRiverConnections(Overworld world)
    {
        int count = 0;
        for (int y = 0; y < world.Height; y++)
        {
            for (int x = 0; x < world.Width; x++)
            {
                foreach (EdgeConnection connection in world.GetEdgeConnections(new WorldCoord(x, y)))
                {
                    if (connection.Type == ConnectionType.River)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static List<(int X, int Y)> CollectRiverSources(IslandPlan plan, IslandDefinition config)
    {
        var candidates = new List<(int X, int Y, float Score)>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast || cell.Elevation < config.RiverMinElevation)
                {
                    continue;
                }

                if (cell.Biome is BiomeId.Ocean or BiomeId.Beach)
                {
                    continue;
                }

                float score = cell.Elevation * 2f + cell.Moisture * 0.4f + cell.VolcanicActivity * 0.5f;
                if (cell.BoundaryType == PlateBoundaryType.Divergent)
                {
                    score += 0.15f;
                }
                else if (cell.BoundaryType == PlateBoundaryType.ConvergentCollision)
                {
                    score += 0.2f;
                }

                candidates.Add((x, y, score));
            }
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));

        var selected = new List<(int X, int Y)>();
        foreach ((int x, int y, _) in candidates)
        {
            if (selected.Count >= config.RiverCount * 3)
            {
                break;
            }

            bool tooClose = selected.Any(source =>
                Math.Abs(source.X - x) + Math.Abs(source.Y - y) < config.RiverHeadSpacing);
            if (tooClose)
            {
                continue;
            }

            selected.Add((x, y));
        }

        return selected;
    }

    private static void TraceRiver(
        Overworld world,
        IslandPlan plan,
        int startX,
        int startY,
        IslandDefinition config,
        HashSet<(int X, int Y)> tracedCells)
    {
        int x = startX;
        int y = startY;
        tracedCells.Add((x, y));

        for (int step = 0; step < config.RiverMaxLength; step++)
        {
            if (!TryFindDownhillNeighbor(plan, x, y, out int nextX, out int nextY, out Direction edge))
            {
                break;
            }

            AddRiverConnection(world, x, y, nextX, nextY, edge, config.RiverWidth);
            tracedCells.Add((nextX, nextY));
            x = nextX;
            y = nextY;

            ref IslandCellData cell = ref plan.GetCell(x, y);
            if (!cell.IsLand || cell.Biome is BiomeId.Ocean or BiomeId.Beach)
            {
                break;
            }
        }
    }

    private static bool TryFindDownhillNeighbor(
        IslandPlan plan,
        int x,
        int y,
        out int nextX,
        out int nextY,
        out Direction edge)
    {
        ref IslandCellData current = ref plan.GetCell(x, y);
        float bestDrop = 0f;
        float bestMoisture = float.MinValue;
        nextX = x;
        nextY = y;
        edge = Direction.North;
        bool found = false;

        foreach ((int neighborX, int neighborY, Direction direction) in Neighbors(x, y, plan.Width, plan.Height))
        {
            ref IslandCellData neighbor = ref plan.GetCell(neighborX, neighborY);
            if (!CanFlowInto(current, neighbor))
            {
                continue;
            }

            float drop = current.Elevation - neighbor.Elevation;
            if (drop < 0f)
            {
                continue;
            }

            if (drop > bestDrop + 0.00001f ||
                (MathF.Abs(drop - bestDrop) <= 0.00001f && neighbor.Moisture > bestMoisture))
            {
                bestDrop = drop;
                bestMoisture = neighbor.Moisture;
                nextX = neighborX;
                nextY = neighborY;
                edge = direction;
                found = true;
            }
        }

        if (!found)
        {
            float lowestElevation = current.Elevation;
            foreach ((int neighborX, int neighborY, Direction direction) in Neighbors(x, y, plan.Width, plan.Height))
            {
                ref IslandCellData neighbor = ref plan.GetCell(neighborX, neighborY);
                if (!CanFlowInto(current, neighbor))
                {
                    continue;
                }

                if (neighbor.Elevation <= lowestElevation + 0.00001f &&
                    (neighbor.Elevation < lowestElevation - 0.00001f ||
                     neighbor.Moisture > bestMoisture ||
                     !neighbor.IsLand))
                {
                    lowestElevation = neighbor.Elevation;
                    bestMoisture = neighbor.Moisture;
                    nextX = neighborX;
                    nextY = neighborY;
                    edge = direction;
                    found = true;
                }
            }
        }

        if (!found && current.IsCoast)
        {
            foreach ((int neighborX, int neighborY, Direction direction) in Neighbors(x, y, plan.Width, plan.Height))
            {
                ref IslandCellData neighbor = ref plan.GetCell(neighborX, neighborY);
                if (neighbor.IsLand)
                {
                    continue;
                }

                nextX = neighborX;
                nextY = neighborY;
                edge = direction;
                return true;
            }
        }

        return found;
    }

    private static bool CanFlowInto(IslandCellData current, IslandCellData neighbor)
    {
        if (neighbor.IsLand)
        {
            return neighbor.Biome is not BiomeId.Ocean;
        }

        return current.IsCoast || current.IsLand;
    }

    private static IEnumerable<(int X, int Y, Direction Edge)> Neighbors(int x, int y, int width, int height)
    {
        if (x > 0)
        {
            yield return (x - 1, y, Direction.West);
        }

        if (x < width - 1)
        {
            yield return (x + 1, y, Direction.East);
        }

        if (y > 0)
        {
            yield return (x, y - 1, Direction.North);
        }

        if (y < height - 1)
        {
            yield return (x, y + 1, Direction.South);
        }
    }

    private static void AddRiverConnection(
        Overworld world,
        int fromX,
        int fromY,
        int toX,
        int toY,
        Direction edge,
        int width)
    {
        int localOffset = edge is Direction.East or Direction.West
            ? LocalMap.Height / 2 - width / 2
            : LocalMap.Width / 2 - width / 2;

        var fromCoord = new WorldCoord(fromX, fromY);
        var toCoord = new WorldCoord(toX, toY);

        if (!SupportsRiver(world.GetCellValue(fromCoord).Biome) && !SupportsRiver(world.GetCellValue(toCoord).Biome))
        {
            return;
        }

        world.AddEdgeConnection(
            fromCoord,
            new EdgeConnection(edge, localOffset, ConnectionType.River, width));
        world.AddEdgeConnection(
            toCoord,
            new EdgeConnection(edge.Opposite(), localOffset, ConnectionType.River, width));
    }

    private static bool SupportsRiver(BiomeId biome)
    {
        return biome is BiomeId.Plains
            or BiomeId.Forest
            or BiomeId.Hills
            or BiomeId.Mountains
            or BiomeId.Swamp
            or BiomeId.Volcanic
            or BiomeId.Beach;
    }

    private static void AddHorizontalRoad(Overworld world, int worldY, int localOffset, int width)
    {
        for (int worldX = 0; worldX < world.Width; worldX++)
        {
            var coord = new WorldCoord(worldX, worldY);
            if (!SupportsRoads(world.GetCellValue(coord).Biome))
            {
                continue;
            }

            if (worldX > 0)
            {
                var westNeighbor = new WorldCoord(worldX - 1, worldY);
                if (SupportsRoads(world.GetCellValue(westNeighbor).Biome))
                {
                    world.AddEdgeConnection(
                        coord,
                        new EdgeConnection(Direction.West, localOffset, ConnectionType.Road, width));
                }
            }

            if (worldX < world.Width - 1)
            {
                var eastNeighbor = new WorldCoord(worldX + 1, worldY);
                if (SupportsRoads(world.GetCellValue(eastNeighbor).Biome))
                {
                    world.AddEdgeConnection(
                        coord,
                        new EdgeConnection(Direction.East, localOffset, ConnectionType.Road, width));
                }
            }
        }
    }

    private static void AddVerticalRoad(Overworld world, int worldX, int localOffset, int width)
    {
        for (int worldY = 0; worldY < world.Height; worldY++)
        {
            var coord = new WorldCoord(worldX, worldY);
            if (!SupportsRoads(world.GetCellValue(coord).Biome))
            {
                continue;
            }

            if (worldY > 0)
            {
                var northNeighbor = new WorldCoord(worldX, worldY - 1);
                if (SupportsRoads(world.GetCellValue(northNeighbor).Biome))
                {
                    world.AddEdgeConnection(
                        coord,
                        new EdgeConnection(Direction.North, localOffset, ConnectionType.Road, width));
                }
            }

            if (worldY < world.Height - 1)
            {
                var southNeighbor = new WorldCoord(worldX, worldY + 1);
                if (SupportsRoads(world.GetCellValue(southNeighbor).Biome))
                {
                    world.AddEdgeConnection(
                        coord,
                        new EdgeConnection(Direction.South, localOffset, ConnectionType.Road, width));
                }
            }
        }
    }

    private static bool SupportsRoads(BiomeId biome)
    {
        return biome is BiomeId.Plains
            or BiomeId.Forest
            or BiomeId.Hills
            or BiomeId.Beach
            or BiomeId.Swamp;
    }
}
