using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class StructureFinalizeStage
{
    private const uint StageSalt = 99;

    public static void Execute(IslandPlan plan, ulong seed, StructureBlueprintCatalog catalog)
    {
        for (int i = 0; i < plan.Structures.Count; i++)
        {
            StructurePlacement structure = plan.Structures[i];
            var blueprint = catalog.Resolve(structure.Type);
            plan.Structures[i] = structure with
            {
                InstanceId = i + 1,
                BlueprintId = blueprint.Id,
                FloorCount = blueprint.FloorCount,
                BasementCount = blueprint.BasementCount
            };
        }

        PruneCoveredPathTiles(plan);
        RepairRoadConnectivity(plan);
        AddDoorSpurs(plan, catalog);
        RepairRoadConnectivity(plan);
        RepairRoadConnectivity(plan);

        if (!GlobalTileConnectivityValidator.IsConnected(plan.RoadGraph.GlobalPathTiles))
        {
            System.Diagnostics.Debug.WriteLine("Road graph remained disconnected after structure finalization.");
        }
    }

    /// <summary>
    /// Removes global road/river tiles that fall inside structure or ruin footprints, so
    /// building stamps never overwrite tiles that the road graph still claims. Roads end at the
    /// footprint edge and the door approach is stamped locally instead.
    /// </summary>
    private static void PruneCoveredPathTiles(IslandPlan plan)
    {
        bool Covered((int GlobalX, int GlobalY) tile)
        {
            foreach (StructurePlacement structure in plan.Structures)
            {
                if (tile.GlobalX >= structure.GlobalOriginX &&
                    tile.GlobalX < structure.GlobalOriginX + structure.Width &&
                    tile.GlobalY >= structure.GlobalOriginY &&
                    tile.GlobalY < structure.GlobalOriginY + structure.Height)
                {
                    return true;
                }
            }

            foreach (RuinSite ruin in plan.RuinSites)
            {
                if (tile.GlobalX >= ruin.GlobalOriginX &&
                    tile.GlobalX < ruin.GlobalOriginX + ruin.Width &&
                    tile.GlobalY >= ruin.GlobalOriginY &&
                    tile.GlobalY < ruin.GlobalOriginY + ruin.Height)
                {
                    return true;
                }
            }

            return false;
        }

        plan.RoadGraph.GlobalPathTiles.RemoveWhere(tile => Covered(tile));
        plan.RiverGraph.GlobalRiverTiles.RemoveWhere(tile => Covered(tile));
    }

    private static void AddDoorSpurs(IslandPlan plan, StructureBlueprintCatalog catalog)
    {
        foreach (StructurePlacement structure in plan.Structures)
        {
            if (structure.Type is StructureType.Helipad)
            {
                continue;
            }

            StructureBlueprintDefinition blueprint = catalog.ResolveById(structure.BlueprintId);
            (int doorX, int doorY) = StructurePlacementQueries.DoorGlobal(structure, blueprint);
            (int approachX, int approachY) = ExteriorApproach(structure, doorX, doorY);

            List<(int GlobalX, int GlobalY)>? spur = FindUnobstructedSpur(
                plan,
                approachX,
                approachY,
                doorX,
                doorY);
            if (spur is not null)
            {
                plan.RoadGraph.AddGlobalPath(spur);
                foreach ((int globalX, int globalY) in spur)
                {
                    int cellX = globalX / LocalMap.Width;
                    int cellY = globalY / LocalMap.Height;
                    if (plan.Contains(cellX, cellY))
                    {
                        IslandPlacementHelper.MarkRole(
                            plan,
                            new Game.Simulation.Coordinates.WorldCoord(cellX, cellY),
                            IslandCellRole.Road);
                    }
                }
            }
        }
    }

    private static void RepairRoadConnectivity(IslandPlan plan)
    {
        List<HashSet<(int GlobalX, int GlobalY)>> components =
            CollectComponents(plan.RoadGraph.GlobalPathTiles)
                .OrderByDescending(component => component.Count)
                .ToList();
        if (components.Count <= 1)
        {
            return;
        }

        HashSet<(int GlobalX, int GlobalY)> connected = components[0];
        for (int i = 1; i < components.Count; i++)
        {
            HashSet<(int GlobalX, int GlobalY)> component = components[i];
            List<(int GlobalX, int GlobalY)>? connector =
                FindComponentConnector(plan, connected, component);
            if (connector is null)
            {
                continue;
            }

            plan.RoadGraph.AddGlobalPath(connector);
            connected.UnionWith(component);
            connected.UnionWith(connector);
        }

        plan.RoadGraph.GlobalPathTiles.RemoveWhere(tile => !connected.Contains(tile));
    }

    private static List<(int GlobalX, int GlobalY)>? FindComponentConnector(
        IslandPlan plan,
        HashSet<(int GlobalX, int GlobalY)> connected,
        HashSet<(int GlobalX, int GlobalY)> component)
    {
        List<(int GlobalX, int GlobalY)>? localRepair =
            FindTerrainAwareTileConnector(plan, connected, component);
        if (localRepair is not null)
        {
            return localRepair;
        }

        int minX = connected.Min(tile => tile.GlobalX);
        int maxX = connected.Max(tile => tile.GlobalX);
        int minY = connected.Min(tile => tile.GlobalY);
        int maxY = connected.Max(tile => tile.GlobalY);

        IEnumerable<(int GlobalX, int GlobalY)> candidates = component
            .OrderBy(tile =>
                DistanceToRange(tile.GlobalX, minX, maxX) +
                DistanceToRange(tile.GlobalY, minY, maxY))
            .Take(128);

        foreach ((int targetX, int targetY) in candidates)
        {
            foreach ((int sourceX, int sourceY) in connected
                         .OrderBy(tile =>
                             Math.Abs(tile.GlobalX - targetX) +
                             Math.Abs(tile.GlobalY - targetY))
                         .Take(128))
            {
                List<(int GlobalX, int GlobalY)> horizontalFirst =
                    BuildOrthogonalPath(sourceX, sourceY, targetX, targetY, horizontalFirst: true);
                if (IsClear(plan, horizontalFirst))
                {
                    return horizontalFirst;
                }

                List<(int GlobalX, int GlobalY)> verticalFirst =
                    BuildOrthogonalPath(sourceX, sourceY, targetX, targetY, horizontalFirst: false);
                if (IsClear(plan, verticalFirst))
                {
                    return verticalFirst;
                }

                List<(int GlobalX, int GlobalY)>? routed = BuildRoutedConnector(
                    plan,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY);
                if (routed is not null && IsClear(plan, routed))
                {
                    return routed;
                }
            }
        }

        return null;
    }

    private static List<(int GlobalX, int GlobalY)>? FindTerrainAwareTileConnector(
        IslandPlan plan,
        HashSet<(int GlobalX, int GlobalY)> connected,
        HashSet<(int GlobalX, int GlobalY)> component)
    {
        const int maxVisited = 500_000;
        var queue = new PriorityQueue<(int X, int Y), float>();
        var cost = new Dictionary<(int X, int Y), float>();
        var parent = new Dictionary<(int X, int Y), (int X, int Y)>();
        foreach ((int x, int y) in component)
        {
            cost[(x, y)] = 0f;
            queue.Enqueue((x, y), 0f);
        }

        while (queue.Count > 0 && cost.Count <= maxVisited)
        {
            (int x, int y) = queue.Dequeue();
            foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (connected.Contains((nx, ny)))
                {
                    var result = new List<(int GlobalX, int GlobalY)> { (nx, ny) };
                    (int X, int Y) cursor = (x, y);
                    while (!component.Contains(cursor))
                    {
                        result.Add(cursor);
                        cursor = parent[cursor];
                    }

                    result.Add(cursor);
                    result.Reverse();
                    return result;
                }

                if (!IsClear(plan, [(nx, ny)]))
                {
                    continue;
                }

                int cellX = nx / LocalMap.Width;
                int cellY = ny / LocalMap.Height;
                int index = cellY * plan.Width + cellX;
                float slope = plan.Slope.Length == plan.Width * plan.Height ? plan.Slope[index] : 0f;
                float hazard = plan.LavaFlowGraph.PathCells.Contains((cellX, cellY)) ? 30f : 0f;
                float candidateCost = cost[(x, y)] + 1f + slope * 8f + hazard;
                if (cost.TryGetValue((nx, ny), out float known) && candidateCost >= known)
                {
                    continue;
                }

                cost[(nx, ny)] = candidateCost;
                parent[(nx, ny)] = (x, y);
                queue.Enqueue((nx, ny), candidateCost);
            }
        }

        return null;
    }

    private static List<(int GlobalX, int GlobalY)>? BuildRoutedConnector(
        IslandPlan plan,
        int sourceX,
        int sourceY,
        int targetX,
        int targetY)
    {
        var sourceCell = new WorldCoord(sourceX / LocalMap.Width, sourceY / LocalMap.Height);
        var targetCell = new WorldCoord(targetX / LocalMap.Width, targetY / LocalMap.Height);
        List<WorldCoord>? route = IslandPathfinder.FindPath(plan, sourceCell, targetCell);
        if (route is null)
        {
            return null;
        }

        var tiles = new HashSet<(int GlobalX, int GlobalY)>();
        (int sourceCenterX, int sourceCenterY) = FacilityRoadGraph.CellCenterGlobal(sourceCell);
        (int targetCenterX, int targetCenterY) = FacilityRoadGraph.CellCenterGlobal(targetCell);
        GlobalTilePathUtility.AddGlobalSegment(
            tiles,
            sourceX,
            sourceY,
            sourceCenterX,
            sourceCenterY,
            width: 1);
        GlobalTilePathUtility.AddPathWithBorderRuns(tiles, route, width: 1);
        GlobalTilePathUtility.AddGlobalSegment(
            tiles,
            targetCenterX,
            targetCenterY,
            targetX,
            targetY,
            width: 1);
        return tiles.ToList();
    }

    private static IEnumerable<HashSet<(int GlobalX, int GlobalY)>> CollectComponents(
        HashSet<(int GlobalX, int GlobalY)> tiles)
    {
        var remaining = new HashSet<(int GlobalX, int GlobalY)>(tiles);
        while (remaining.Count > 0)
        {
            (int GlobalX, int GlobalY) first = remaining.First();
            var component = new HashSet<(int GlobalX, int GlobalY)> { first };
            var queue = new Queue<(int GlobalX, int GlobalY)>();
            queue.Enqueue(first);
            remaining.Remove(first);

            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                foreach ((int nx, int ny) in new[]
                         {
                             (x + 1, y),
                             (x - 1, y),
                             (x, y + 1),
                             (x, y - 1)
                         })
                {
                    if (remaining.Remove((nx, ny)))
                    {
                        component.Add((nx, ny));
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            yield return component;
        }
    }

    private static int DistanceToRange(int value, int min, int max)
    {
        return value < min ? min - value : value > max ? value - max : 0;
    }

    private static List<(int GlobalX, int GlobalY)>? FindUnobstructedSpur(
        IslandPlan plan,
        int approachX,
        int approachY,
        int doorX,
        int doorY)
    {
        foreach ((int roadX, int roadY) in plan.RoadGraph.GlobalPathTiles
                     .OrderBy(tile => Math.Abs(tile.GlobalX - approachX) + Math.Abs(tile.GlobalY - approachY)))
        {
            List<(int GlobalX, int GlobalY)> horizontalFirst =
                BuildOrthogonalPath(roadX, roadY, approachX, approachY, horizontalFirst: true);
            if (IsClear(plan, horizontalFirst))
            {
                horizontalFirst.Add((doorX, doorY));
                return horizontalFirst;
            }

            List<(int GlobalX, int GlobalY)> verticalFirst =
                BuildOrthogonalPath(roadX, roadY, approachX, approachY, horizontalFirst: false);
            if (IsClear(plan, verticalFirst))
            {
                verticalFirst.Add((doorX, doorY));
                return verticalFirst;
            }
        }

        return null;
    }

    private static List<(int GlobalX, int GlobalY)> BuildOrthogonalPath(
        int fromX,
        int fromY,
        int toX,
        int toY,
        bool horizontalFirst)
    {
        var result = new List<(int GlobalX, int GlobalY)>();
        int x = fromX;
        int y = fromY;
        result.Add((x, y));

        void WalkX()
        {
            while (x != toX)
            {
                x += Math.Sign(toX - x);
                result.Add((x, y));
            }
        }

        void WalkY()
        {
            while (y != toY)
            {
                y += Math.Sign(toY - y);
                result.Add((x, y));
            }
        }

        if (horizontalFirst)
        {
            WalkX();
            WalkY();
        }
        else
        {
            WalkY();
            WalkX();
        }

        return result;
    }

    private static bool IsClear(
        IslandPlan plan,
        IEnumerable<(int GlobalX, int GlobalY)> path)
    {
        foreach ((int x, int y) in path)
        {
            if (x < 0 || y < 0 ||
                x >= plan.Width * LocalMap.Width ||
                y >= plan.Height * LocalMap.Height)
            {
                return false;
            }

            int cellX = x / LocalMap.Width;
            int cellY = y / LocalMap.Height;
            if (!plan.IsLand(cellX, cellY)
                || plan.VolcanoExclusion.IsProtected(cellX, cellY))
            {
                return false;
            }

            if (StructurePlacementQueries.FindAtGlobalTile(plan, x, y) is not null)
            {
                return false;
            }

            foreach (RuinSite ruin in plan.RuinSites)
            {
                if (x >= ruin.GlobalOriginX && x < ruin.GlobalOriginX + ruin.Width &&
                    y >= ruin.GlobalOriginY && y < ruin.GlobalOriginY + ruin.Height)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static (int X, int Y) ExteriorApproach(
        StructurePlacement structure,
        int doorX,
        int doorY)
    {
        if (doorY == structure.GlobalOriginY)
        {
            return (doorX, doorY - 1);
        }

        if (doorY == structure.GlobalOriginY + structure.Height - 1)
        {
            return (doorX, doorY + 1);
        }

        if (doorX == structure.GlobalOriginX)
        {
            return (doorX - 1, doorY);
        }

        return (doorX + 1, doorY);
    }
}
