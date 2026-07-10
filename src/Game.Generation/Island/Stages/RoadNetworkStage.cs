using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Generation.Regional;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class RoadNetworkStage
{
    private const uint StageSalt = 3;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        plan.RoadGraph.Nodes.Clear();
        plan.RoadGraph.Segments.Clear();
        plan.RoadGraph.PathCells.Clear();
        plan.RoadGraph.GlobalPathTiles.Clear();

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        List<WorldCoord> ringCells = PickVolcanoRingCells(plan, config);
        WorldCoord? hubCell = ResolveHubCell(plan, ringCells);
        if (hubCell is null)
        {
            return;
        }

        int hubId = AddNode(plan, hubCell.Value, FacilityRoadNodeKind.Hub);
        var connected = BuildRingRoad(plan, config, hubId, hubCell.Value, ringCells);
        List<WorldCoord> junctionCells = PickJunctionCells(plan, hubCell.Value, config.RoadNetworkJunctionCount, stageSeed);

        var unconnected = junctionCells
            .Select((cell, index) => (Cell: cell, NodeId: AddNode(plan, cell, FacilityRoadNodeKind.Junction)))
            .ToList();

        while (unconnected.Count > 0)
        {
            int bestNodeId = -1;
            (WorldCoord Cell, int NodeId) bestTarget = default;
            List<WorldCoord>? bestPath = null;
            IEnumerable<(int SourceId, WorldCoord Source, WorldCoord Target, int TargetId)> candidates =
                from target in unconnected
                from sourceId in connected
                let source = plan.RoadGraph.Nodes[sourceId].Cell
                orderby Square(source.X - target.Cell.X) + Square(source.Y - target.Cell.Y),
                    target.NodeId,
                    sourceId
                select (sourceId, source, target.Cell, target.NodeId);

            foreach ((int sourceId, WorldCoord source, WorldCoord target, int targetId) in candidates)
            {
                List<WorldCoord>? path = IslandPathfinder.FindPath(plan, source, target);
                if (path is null)
                {
                    continue;
                }

                bestNodeId = sourceId;
                bestTarget = (target, targetId);
                bestPath = path;
                break;
            }

            if (bestPath is null)
            {
                break;
            }

            var segment = new FacilityRoadSegment { FromNodeId = bestNodeId, ToNodeId = bestTarget.NodeId };
            segment.Path.AddRange(bestPath);
            plan.RoadGraph.Segments.Add(segment);
            plan.RoadGraph.AddPath(bestPath);
            AddGlobalCenterline(plan, bestPath, config.RoadWidth);

            connected.Add(bestTarget.NodeId);
            unconnected.RemoveAll(entry => entry.NodeId == bestTarget.NodeId);
        }

        List<WorldCoord> coastConnectors = PickCoastConnectors(plan, config.DockCount + 2, stageSeed ^ 0xC04AUL);
        foreach (WorldCoord coastCell in coastConnectors)
        {
            int nodeId = AddNode(plan, coastCell, FacilityRoadNodeKind.CoastConnector);
            (int sourceId, List<WorldCoord>? path) = FindBestConnection(plan, connected, coastCell);
            if (path is null)
            {
                continue;
            }

            var segment = new FacilityRoadSegment { FromNodeId = sourceId, ToNodeId = nodeId };
            segment.Path.AddRange(path);
            plan.RoadGraph.Segments.Add(segment);
            plan.RoadGraph.AddPath(path);
            AddGlobalCenterline(plan, path, config.RoadWidth);
        }

        MarkRoadRoles(plan);
        StampRoadBiomes(plan);
        ValidateRoadConnectivity(plan);
    }

    public static void AddStructureSpur(IslandPlan plan, WorldCoord structureCell, IslandDefinition config)
    {
        WorldCoord? nearestRoad = plan.RoadGraph.FindNearestRoadCell(structureCell);
        if (nearestRoad is null)
        {
            return;
        }

        List<WorldCoord>? route = IslandPathfinder.FindPath(plan, nearestRoad.Value, structureCell);
        if (route is null)
        {
            return;
        }

        plan.RoadGraph.AddPath(route);
        GlobalTilePathUtility.AddPathWithBorderRuns(plan.RoadGraph.GlobalPathTiles, route, config.RoadWidth);
        foreach (WorldCoord cell in route)
        {
            if (plan.Contains(cell.X, cell.Y))
            {
                IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Road);
            }
        }

        StampRoadBiomes(plan);
    }

    private static WorldCoord? ResolveHubCell(IslandPlan plan, IReadOnlyList<WorldCoord> ringCells)
    {
        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        if (ringCells.Count > 0)
        {
            return ringCells
                .OrderBy(cell => Square(cell.X - centerX) + Square(cell.Y - centerY))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
        }

        return Enumerable.Range(0, plan.Height)
            .SelectMany(y => Enumerable.Range(0, plan.Width).Select(x => new WorldCoord(x, y)))
            .Where(cell => plan.IsLand(cell.X, cell.Y)
                && !plan.GetCell(cell).IsCoast
                && !plan.VolcanoExclusion.IsProtected(cell.X, cell.Y))
            .OrderBy(cell => Square(cell.X - centerX) + Square(cell.Y - centerY))
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .Cast<WorldCoord?>()
            .FirstOrDefault();
    }

    private static List<WorldCoord> PickVolcanoRingCells(IslandPlan plan, IslandDefinition config)
    {
        if (plan.VolcanoExclusion.Zones.Count == 0 || config.VolcanoRoadRingNodes < 3)
        {
            return [];
        }

        VolcanoExclusionZone zone = plan.VolcanoExclusion.Zones[0];
        float targetRadius = Math.Max(zone.ProtectedRadius + 0.12f, config.VolcanoRoadRingRadius);
        var eligible = new List<(WorldCoord Cell, float Angle, float Radius)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast || cell.Biome is BiomeId.Mountains or BiomeId.Swamp)
                {
                    continue;
                }

                float radius = zone.NormalizedDistance(x, y);
                if (radius <= zone.ProtectedRadius || MathF.Abs(radius - targetRadius) > 0.45f)
                {
                    continue;
                }

                eligible.Add((
                    new WorldCoord(x, y),
                    MathF.Atan2(y - zone.CenterY, x - zone.CenterX),
                    radius));
            }
        }

        var result = new List<WorldCoord>();
        for (int i = 0; i < config.VolcanoRoadRingNodes; i++)
        {
            float desired = -MathF.PI + MathF.Tau * i / config.VolcanoRoadRingNodes;
            WorldCoord? pick = eligible
                .Where(entry => !result.Contains(entry.Cell))
                .OrderBy(entry => AngularDistance(entry.Angle, desired) * 4f + MathF.Abs(entry.Radius - targetRadius))
                .ThenBy(entry => entry.Cell.Y)
                .ThenBy(entry => entry.Cell.X)
                .Select(entry => (WorldCoord?)entry.Cell)
                .FirstOrDefault();
            if (pick is not null)
            {
                result.Add(pick.Value);
            }
        }

        return result
            .OrderBy(cell => MathF.Atan2(cell.Y - zone.CenterY, cell.X - zone.CenterX))
            .ToList();
    }

    private static HashSet<int> BuildRingRoad(
        IslandPlan plan,
        IslandDefinition config,
        int hubId,
        WorldCoord hubCell,
        IReadOnlyList<WorldCoord> ringCells)
    {
        var connected = new HashSet<int> { hubId };
        if (ringCells.Count < 3)
        {
            return connected;
        }

        int hubIndex = ringCells.ToList().IndexOf(hubCell);
        if (hubIndex < 0)
        {
            return connected;
        }

        var ordered = Enumerable.Range(0, ringCells.Count)
            .Select(offset => ringCells[(hubIndex + offset) % ringCells.Count])
            .ToList();
        int previousId = hubId;
        WorldCoord previousCell = hubCell;

        foreach (WorldCoord cell in ordered.Skip(1))
        {
            List<WorldCoord>? path = IslandPathfinder.FindPath(plan, previousCell, cell);
            if (path is null)
            {
                continue;
            }

            int nodeId = AddNode(plan, cell, FacilityRoadNodeKind.Ring);
            AddSegment(plan, config, previousId, nodeId, path);
            connected.Add(nodeId);
            previousId = nodeId;
            previousCell = cell;
        }

        if (connected.Count >= 3)
        {
            List<WorldCoord>? closingPath = IslandPathfinder.FindPath(plan, previousCell, hubCell);
            if (closingPath is not null)
            {
                AddSegment(plan, config, previousId, hubId, closingPath);
            }
        }

        return connected;
    }

    private static (int SourceId, List<WorldCoord>? Path) FindBestConnection(
        IslandPlan plan,
        IEnumerable<int> sourceIds,
        WorldCoord target)
    {
        int bestId = -1;
        List<WorldCoord>? bestPath = null;
        foreach (int sourceId in sourceIds
                     .OrderBy(id =>
                     {
                         WorldCoord source = plan.RoadGraph.Nodes[id].Cell;
                         return Square(source.X - target.X) + Square(source.Y - target.Y);
                     })
                     .ThenBy(id => id))
        {
            WorldCoord source = plan.RoadGraph.Nodes[sourceId].Cell;
            List<WorldCoord>? candidate = IslandPathfinder.FindPath(plan, source, target);
            if (candidate is not null)
            {
                bestId = sourceId;
                bestPath = candidate;
                break;
            }
        }

        return (bestId, bestPath);
    }

    private static void AddSegment(
        IslandPlan plan,
        IslandDefinition config,
        int fromId,
        int toId,
        IReadOnlyList<WorldCoord> path)
    {
        var segment = new FacilityRoadSegment { FromNodeId = fromId, ToNodeId = toId };
        segment.Path.AddRange(path);
        plan.RoadGraph.Segments.Add(segment);
        plan.RoadGraph.AddPath(path);
        AddGlobalCenterline(plan, path, config.RoadWidth);
    }

    private static float Square(float value) => value * value;

    private static float AngularDistance(float left, float right)
    {
        float difference = MathF.Abs(left - right) % MathF.Tau;
        return MathF.Min(difference, MathF.Tau - difference);
    }

    private static int AddNode(IslandPlan plan, WorldCoord cell, FacilityRoadNodeKind kind)
    {
        int id = plan.RoadGraph.Nodes.Count;
        plan.RoadGraph.Nodes.Add(new FacilityRoadNode
        {
            Id = id,
            Cell = cell,
            Kind = kind
        });
        return id;
    }

    private static List<WorldCoord> PickJunctionCells(
        IslandPlan plan,
        WorldCoord hubCell,
        int count,
        ulong stageSeed)
    {
        var candidates = new List<(WorldCoord Cell, float Score)>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsCoast
                    || plan.VolcanoExclusion.IsProtected(x, y)
                    || plan.LavaFlowGraph.PathCells.Contains((x, y))
                    || cell.Biome is BiomeId.Ocean or BiomeId.ShallowWater or BiomeId.Reef or BiomeId.Mountains or BiomeId.Swamp)
                {
                    continue;
                }

                int regionId = plan.GetRegionId(x, y);
                IslandRegion? region = plan.Regions.FirstOrDefault(r => r.Id == regionId);
                if (region is null || !region.IsMainIsland)
                {
                    continue;
                }

                var coord = new WorldCoord(x, y);
                int dx = x - hubCell.X;
                int dy = y - hubCell.Y;
                float score = MathF.Sqrt(dx * dx + dy * dy);
                if (cell.Biome == BiomeId.Plains)
                {
                    score *= 0.85f;
                }

                candidates.Add((coord, score));
            }
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        var random = new DeterministicRandom(stageSeed);
        var picked = new List<WorldCoord>();
        foreach ((WorldCoord cell, _) in candidates)
        {
            if (picked.Count >= count)
            {
                break;
            }

            bool tooClose = picked.Any(existing =>
                Math.Abs(existing.X - cell.X) + Math.Abs(existing.Y - cell.Y) < 8);
            if (tooClose)
            {
                continue;
            }

            if (random.NextFloat() > 0.35f || picked.Count < count / 2)
            {
                picked.Add(cell);
            }
        }

        while (picked.Count < count && picked.Count < candidates.Count)
        {
            WorldCoord cell = candidates[picked.Count].Cell;
            if (!picked.Contains(cell))
            {
                picked.Add(cell);
            }
        }

        return picked;
    }

    private static List<WorldCoord> PickCoastConnectors(IslandPlan plan, int count, ulong stageSeed)
    {
        List<WorldCoord> coastCells = IslandPlacementHelper.FindCoastalCells(plan);
        return IslandPlacementHelper.PickSpreadCells(coastCells, count, stageSeed);
    }

    private static void AddGlobalCenterline(IslandPlan plan, IReadOnlyList<WorldCoord> path, int width)
    {
        GlobalTilePathUtility.AddPathWithBorderRuns(plan.RoadGraph.GlobalPathTiles, path, width);
    }

    private static void ValidateRoadConnectivity(IslandPlan plan)
    {
        if (plan.RoadGraph.GlobalPathTiles.Count == 0)
        {
            return;
        }

        int connected = GlobalTileConnectivityValidator.CountConnectedTiles(
            plan.RoadGraph.GlobalPathTiles);
        if (connected != plan.RoadGraph.GlobalPathTiles.Count)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Road network has {plan.RoadGraph.GlobalPathTiles.Count - connected} disconnected global tiles.");
        }
    }

    private static void MarkRoadRoles(IslandPlan plan)
    {
        foreach ((int x, int y) in plan.RoadGraph.PathCells)
        {
            IslandPlacementHelper.MarkRole(plan, new WorldCoord(x, y), IslandCellRole.Road);
        }
    }

    private static void StampRoadBiomes(IslandPlan plan)
    {
        foreach ((int x, int y) in plan.RoadGraph.PathCells)
        {
            if (!plan.Contains(x, y) || !plan.IsLand(x, y))
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(x, y);
            if (!cell.IsCoast)
            {
                cell.Biome = BiomeId.Plains;
            }
        }
    }
}
