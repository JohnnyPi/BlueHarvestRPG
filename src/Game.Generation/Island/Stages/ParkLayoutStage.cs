using Game.Content.Definitions;
using Game.Generation.Island.Stages;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class ParkLayoutStage
{
    private const uint StageSalt = 4;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        WorldCoord? visitorCell = ResolveHubCell(plan);
        if (visitorCell is null)
        {
            return;
        }

        plan.VisitorCenterCell = visitorCell.Value;
        plan.VisitorCenterRegionId = plan.GetRegionId(visitorCell.Value.X, visitorCell.Value.Y);
        IslandPlacementHelper.MarkRole(plan, visitorCell.Value, IslandCellRole.VisitorCenter);

        (int visitorGx, int visitorGy) = IslandPlacementHelper.CenteredOrigin(visitorCell.Value, 28, 24);
        plan.Structures.Add(StructurePlacement.CreatePending(
            StructureType.VisitorCenter,
            visitorGx,
            visitorGy,
            28,
            24));
        RoadNetworkStage.AddStructureSpur(plan, visitorCell.Value, config);

        List<WorldCoord> coastCells = IslandPlacementHelper.FindCoastalCells(plan);
        List<WorldCoord> dockCells = PickRoadConnectedCoastCells(plan, coastCells, config.DockCount, stageSeed ^ 0xD0C001UL);

        foreach (WorldCoord dockCell in dockCells)
        {
            IslandPlacementHelper.MarkRole(plan, dockCell, IslandCellRole.Dock);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(dockCell, 20, 12);
            plan.Structures.Add(StructurePlacement.CreatePending(StructureType.Dock, gx, gy, 20, 12));
            RoadNetworkStage.AddStructureSpur(plan, dockCell, config);
        }

        List<WorldCoord> roadAdjacentCandidates = CollectRoadAdjacentCandidates(plan, visitorCell.Value);
        PlaceNearRoad(
            plan,
            config,
            visitorCell.Value,
            roadAdjacentCandidates,
            StructureType.Helipad,
            IslandCellRole.Helipad,
            config.HelipadCount,
            10,
            10,
            random);
        PlaceNearRoad(
            plan,
            config,
            visitorCell.Value,
            roadAdjacentCandidates,
            StructureType.Hotel,
            IslandCellRole.Hotel,
            config.HotelCount,
            24,
            18,
            random);
        PlaceNearRoad(
            plan,
            config,
            visitorCell.Value,
            roadAdjacentCandidates,
            StructureType.Restaurant,
            IslandCellRole.Restaurant,
            config.RestaurantCount,
            14,
            12,
            random);
        PlaceNearRoad(
            plan,
            config,
            visitorCell.Value,
            roadAdjacentCandidates,
            StructureType.Attraction,
            IslandCellRole.Attraction,
            config.AttractionCount,
            18,
            18,
            random);

        if (!plan.Structures.Any(s => s.Type == StructureType.Attraction))
        {
            foreach (WorldCoord cell in PickRoadAdjacentFallback(plan, config.AttractionCount, stageSeed ^ 0xA77AC701UL))
            {
                if (plan.GetCell(cell).Role is not (IslandCellRole.None or IslandCellRole.Coast))
                {
                    continue;
                }

                IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Attraction);
                (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 18, 18);
                plan.Structures.Add(StructurePlacement.CreatePending(StructureType.Attraction, gx, gy, 18, 18));
                RoadNetworkStage.AddStructureSpur(plan, cell, config);
            }
        }
    }

    private static WorldCoord? ResolveHubCell(IslandPlan plan)
    {
        FacilityRoadNode? hub = plan.RoadGraph.Nodes.FirstOrDefault(node => node.Kind == FacilityRoadNodeKind.Hub);
        if (hub is not null)
        {
            return hub.Cell;
        }

        return IslandPlacementHelper.FindCentralMainIslandCell(plan);
    }

    private static List<WorldCoord> PickRoadConnectedCoastCells(
        IslandPlan plan,
        List<WorldCoord> coastCells,
        int count,
        ulong stageSeed)
    {
        var roadCoast = coastCells
            .Where(cell => plan.RoadGraph.PathCells.Contains((cell.X, cell.Y)) ||
                           plan.RoadGraph.IsAdjacentToRoad(cell))
            .ToList();

        if (roadCoast.Count >= count)
        {
            return IslandPlacementHelper.PickSpreadCells(roadCoast, count, stageSeed);
        }

        return IslandPlacementHelper.PickSpreadCells(coastCells, count, stageSeed);
    }

    private static List<WorldCoord> CollectRoadAdjacentCandidates(IslandPlan plan, WorldCoord visitorCell)
    {
        var candidates = new List<WorldCoord>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsCoast || cell.Role != IslandCellRole.None && cell.Role != IslandCellRole.Coast)
                {
                    continue;
                }

                if (coord == visitorCell || !plan.RoadGraph.IsAdjacentToRoad(coord))
                {
                    continue;
                }

                if (plan.GetRegionId(x, y) == plan.VisitorCenterRegionId)
                {
                    candidates.Add(coord);
                }
            }
        }

        if (candidates.Count == 0)
        {
            for (int y = 0; y < plan.Height; y++)
            {
                for (int x = 0; x < plan.Width; x++)
                {
                    var coord = new WorldCoord(x, y);
                    if (plan.IsLand(x, y) && plan.RoadGraph.IsAdjacentToRoad(coord))
                    {
                        ref IslandCellData cell = ref plan.GetCell(x, y);
                        if (cell.Role is IslandCellRole.None or IslandCellRole.Coast)
                        {
                            candidates.Add(coord);
                        }
                    }
                }
            }
        }

        return candidates;
    }

    private static List<WorldCoord> PickRoadAdjacentFallback(IslandPlan plan, int count, ulong stageSeed)
    {
        var candidates = new List<WorldCoord>();
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                var coord = new WorldCoord(x, y);
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.Role is not (IslandCellRole.None or IslandCellRole.Coast))
                {
                    continue;
                }

                if (plan.RoadGraph.IsAdjacentToRoad(coord))
                {
                    candidates.Add(coord);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return IslandPlacementHelper.SampleLandCells(
                plan,
                cell => cell.Role is IslandCellRole.None or IslandCellRole.Coast,
                count,
                stageSeed);
        }

        return IslandPlacementHelper.PickSpreadCells(candidates, count, stageSeed);
    }

    private static void PlaceNearRoad(
        IslandPlan plan,
        IslandDefinition config,
        WorldCoord visitorCell,
        List<WorldCoord> candidates,
        StructureType type,
        IslandCellRole role,
        int count,
        int width,
        int height,
        DeterministicRandom random)
    {
        var sorted = candidates
            .Select(c =>
            {
                int dx = c.X - visitorCell.X;
                int dy = c.Y - visitorCell.Y;
                return (Cell: c, Score: dx * dx + dy * dy + random.NextFloat() * 4f);
            })
            .OrderBy(entry => entry.Score)
            .Select(entry => entry.Cell)
            .ToList();

        int placed = 0;
        foreach (WorldCoord cell in sorted)
        {
            if (placed >= count)
            {
                break;
            }

            if (plan.GetCell(cell).Role != IslandCellRole.None && plan.GetCell(cell).Role != IslandCellRole.Coast)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, role);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, width, height);
            plan.Structures.Add(StructurePlacement.CreatePending(type, gx, gy, width, height));
            RoadNetworkStage.AddStructureSpur(plan, cell, config);
            placed++;
        }

        if (placed >= count)
        {
            return;
        }

        foreach (WorldCoord cell in PickRoadAdjacentFallback(plan, count - placed, (ulong)(type.GetHashCode() + placed + 17)))
        {
            if (placed >= count)
            {
                break;
            }

            if (plan.GetCell(cell).Role != IslandCellRole.None && plan.GetCell(cell).Role != IslandCellRole.Coast)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, role);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, width, height);
            plan.Structures.Add(StructurePlacement.CreatePending(type, gx, gy, width, height));
            RoadNetworkStage.AddStructureSpur(plan, cell, config);
            placed++;
        }
    }
}
