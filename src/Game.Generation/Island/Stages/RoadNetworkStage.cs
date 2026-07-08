using Game.Content.Definitions;
using Game.Generation.Noise;
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

        WorldCoord? hubCell = IslandPlacementHelper.FindCentralMainIslandCell(plan);
        if (hubCell is null)
        {
            return;
        }

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        int hubId = AddNode(plan, hubCell.Value, FacilityRoadNodeKind.Hub);
        List<WorldCoord> junctionCells = PickJunctionCells(plan, hubCell.Value, config.RoadNetworkJunctionCount, stageSeed);

        var connected = new HashSet<int> { hubId };
        var unconnected = junctionCells
            .Select((cell, index) => (Cell: cell, NodeId: AddNode(plan, cell, FacilityRoadNodeKind.Junction)))
            .ToList();

        while (unconnected.Count > 0)
        {
            int bestNodeId = -1;
            (WorldCoord Cell, int NodeId) bestTarget = default;
            List<WorldCoord>? bestPath = null;
            int bestCost = int.MaxValue;

            foreach ((WorldCoord cell, int nodeId) in unconnected)
            {
                foreach (int sourceId in connected)
                {
                    WorldCoord sourceCell = plan.RoadGraph.Nodes.First(n => n.Id == sourceId).Cell;
                    List<WorldCoord>? path = IslandPathfinder.FindPath(plan, sourceCell, cell);
                    if (path is null)
                    {
                        continue;
                    }

                    int cost = path.Count;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestNodeId = sourceId;
                        bestTarget = (cell, nodeId);
                        bestPath = path;
                    }
                }
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
            WorldCoord anchor = hubCell.Value;
            List<WorldCoord>? path = IslandPathfinder.FindPath(plan, anchor, coastCell);
            if (path is null)
            {
                continue;
            }

            var segment = new FacilityRoadSegment { FromNodeId = hubId, ToNodeId = nodeId };
            segment.Path.AddRange(path);
            plan.RoadGraph.Segments.Add(segment);
            plan.RoadGraph.AddPath(path);
            AddGlobalCenterline(plan, path, config.RoadWidth);
        }

        MarkRoadRoles(plan);
        StampRoadBiomes(plan);
    }

    public static void AddStructureSpur(IslandPlan plan, WorldCoord structureCell, IslandDefinition config)
    {
        WorldCoord? nearestRoad = plan.RoadGraph.FindNearestRoadCell(structureCell);
        if (nearestRoad is null)
        {
            return;
        }

        plan.RoadGraph.AddSpur(nearestRoad.Value, structureCell);
        IslandPlacementHelper.MarkRole(plan, structureCell, IslandCellRole.Road);
        StampRoadBiomes(plan);
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
                if (cell.IsCoast || cell.Biome is BiomeId.Ocean or BiomeId.Mountains or BiomeId.Swamp)
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
        for (int i = 0; i < path.Count; i++)
        {
            WorldCoord cell = path[i];
            (int centerGx, int centerGy) = FacilityRoadGraph.CellCenterGlobal(cell);
            plan.RoadGraph.GlobalPathTiles.Add((centerGx, centerGy));

            if (width > 1)
            {
                plan.RoadGraph.GlobalPathTiles.Add((centerGx, centerGy + 1));
            }

            if (i > 0)
            {
                WorldCoord previous = path[i - 1];
                if (previous.X != cell.X)
                {
                    int stepGx = previous.X < cell.X
                        ? cell.X * Game.Simulation.LocalMaps.LocalMap.Width
                        : cell.X * Game.Simulation.LocalMaps.LocalMap.Width + Game.Simulation.LocalMaps.LocalMap.Width - 1;
                    plan.RoadGraph.GlobalPathTiles.Add((stepGx, centerGy));
                }

                if (previous.Y != cell.Y)
                {
                    int stepGy = previous.Y < cell.Y
                        ? cell.Y * Game.Simulation.LocalMaps.LocalMap.Height
                        : cell.Y * Game.Simulation.LocalMaps.LocalMap.Height + Game.Simulation.LocalMaps.LocalMap.Height - 1;
                    plan.RoadGraph.GlobalPathTiles.Add((centerGx, stepGy));
                }
            }
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
