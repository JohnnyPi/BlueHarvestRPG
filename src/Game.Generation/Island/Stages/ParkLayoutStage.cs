using Game.Content.Definitions;
using Game.Generation.Island.Stages;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class ParkLayoutStage
{
    private const uint StageSalt = 4;

    public static void Execute(
        IslandPlan plan,
        IslandDefinition config,
        ulong seed,
        StructureBlueprintCatalog? blueprintCatalog = null)
    {
        blueprintCatalog ??= StructureBlueprintCatalogDefaults.Create();
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        WorldCoord? hubCell = ResolveHubCell(plan);
        if (hubCell is null)
        {
            return;
        }

        WorldCoord visitorSite = PickVisitorSiteNearHub(plan, hubCell.Value, stageSeed ^ 0x715CUL);
        plan.VisitorCenterCell = visitorSite;
        plan.VisitorCenterRegionId = plan.GetRegionId(visitorSite.X, visitorSite.Y);

        TryPlaceStructure(
            plan,
            config,
            visitorSite,
            StructureType.VisitorCenter,
            IslandCellRole.VisitorCenter,
            largeWidth: 96,
            largeHeight: 80,
            fallbackWidth: 28,
            fallbackHeight: 24,
            blueprintCatalog: blueprintCatalog);

        StructurePlacement? visitorStructure = plan.Structures
            .LastOrDefault(structure => structure.Type == StructureType.VisitorCenter);
        if (visitorStructure is not null)
        {
            plan.VisitorCenterCell = StructurePlacementQueries.OriginCell(visitorStructure);
        }

        List<WorldCoord> coastCells = IslandPlacementHelper.FindCoastalCells(plan);
        List<WorldCoord> dockCells = PickRoadConnectedCoastCells(plan, coastCells, config.DockCount, stageSeed ^ 0xD0C001UL);

        foreach (WorldCoord dockCell in dockCells)
        {
            IslandPlacementHelper.MarkRole(plan, dockCell, IslandCellRole.Dock);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(dockCell, 20, 12);
            StructurePlacement structure = StructurePlacement.CreatePending(StructureType.Dock, gx, gy, 20, 12);
            plan.Structures.Add(structure);
            AddStructureDoorSpur(plan, structure, config, blueprintCatalog);
        }

        List<WorldCoord> roadAdjacentCandidates = CollectRoadAdjacentCandidates(plan, hubCell.Value);
        PlaceNearRoad(
            plan,
            config,
            hubCell.Value,
            roadAdjacentCandidates,
            StructureType.Helipad,
            IslandCellRole.Helipad,
            config.HelipadCount,
            10,
            10,
            random,
            blueprintCatalog);
        PlaceNearRoad(
            plan,
            config,
            hubCell.Value,
            roadAdjacentCandidates,
            StructureType.Hotel,
            IslandCellRole.Hotel,
            config.HotelCount,
            96,
            72,
            24,
            18,
            random,
            blueprintCatalog);
        PlaceNearRoad(
            plan,
            config,
            hubCell.Value,
            roadAdjacentCandidates,
            StructureType.Restaurant,
            IslandCellRole.Restaurant,
            config.RestaurantCount,
            14,
            12,
            random,
            blueprintCatalog);
        PlaceNearRoad(
            plan,
            config,
            hubCell.Value,
            roadAdjacentCandidates,
            StructureType.Attraction,
            IslandCellRole.Attraction,
            config.AttractionCount,
            18,
            18,
            random,
            blueprintCatalog);

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
                StructurePlacement structure =
                    StructurePlacement.CreatePending(StructureType.Attraction, gx, gy, 18, 18);
                plan.Structures.Add(structure);
                AddStructureDoorSpur(plan, structure, config, blueprintCatalog);
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

    private static WorldCoord PickVisitorSiteNearHub(IslandPlan plan, WorldCoord hubCell, ulong stageSeed)
    {
        var candidates = new List<WorldCoord>();
        for (int dy = -4; dy <= 4; dy++)
        {
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = hubCell.X + dx;
                int y = hubCell.Y + dy;
                if (!plan.Contains(x, y) || !plan.IsLand(x, y) || plan.GetCell(x, y).IsCoast)
                {
                    continue;
                }

                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                candidates.Add(new WorldCoord(x, y));
            }
        }

        if (candidates.Count == 0)
        {
            return hubCell;
        }

        return IslandPlacementHelper.PickSpreadCells(candidates, 1, stageSeed).First();
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

    private static bool TryPlaceStructure(
        IslandPlan plan,
        IslandDefinition config,
        WorldCoord anchorCell,
        StructureType type,
        IslandCellRole role,
        int largeWidth,
        int largeHeight,
        int fallbackWidth,
        int fallbackHeight,
        StructureBlueprintCatalog blueprintCatalog)
    {
        var candidates = CollectPlacementCandidates(plan, anchorCell, radius: 14);
        if (TryPlaceFromCandidates(
                plan,
                config,
                candidates,
                type,
                role,
                largeWidth,
                largeHeight,
                blueprintCatalog))
        {
            return true;
        }

        return TryPlaceFromCandidates(
            plan,
            config,
            candidates,
            type,
            role,
            fallbackWidth,
            fallbackHeight,
            blueprintCatalog);
    }

    private static bool TryPlaceFromCandidates(
        IslandPlan plan,
        IslandDefinition config,
        IEnumerable<WorldCoord> candidates,
        StructureType type,
        IslandCellRole role,
        int width,
        int height,
        StructureBlueprintCatalog blueprintCatalog)
    {
        foreach (WorldCoord cell in candidates)
        {
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, width, height);
            if (!IslandPlacementHelper.CanPlaceFootprint(plan, gx, gy, width, height))
            {
                continue;
            }

            IslandPlacementHelper.MarkFootprintRoles(plan, gx, gy, width, height, role);
            StructurePlacement structure = StructurePlacement.CreatePending(type, gx, gy, width, height);
            plan.Structures.Add(structure);
            AddStructureDoorSpur(plan, structure, config, blueprintCatalog);
            return true;
        }

        return false;
    }

    private static List<WorldCoord> CollectPlacementCandidates(IslandPlan plan, WorldCoord anchor, int radius)
    {
        var candidates = new List<(WorldCoord Cell, float Score)> { (anchor, 0f) };

        for (int y = anchor.Y - radius; y <= anchor.Y + radius; y++)
        {
            for (int x = anchor.X - radius; x <= anchor.X + radius; x++)
            {
                if (!plan.Contains(x, y) || !plan.IsLand(x, y) || plan.GetCell(x, y).IsCoast)
                {
                    continue;
                }

                int dx = x - anchor.X;
                int dy = y - anchor.Y;
                candidates.Add((new WorldCoord(x, y), dx * dx + dy * dy));
            }
        }

        return candidates
            .OrderBy(entry => entry.Score)
            .Select(entry => entry.Cell)
            .ToList();
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
        DeterministicRandom random,
        StructureBlueprintCatalog blueprintCatalog)
    {
        PlaceNearRoad(
            plan,
            config,
            visitorCell,
            candidates,
            type,
            role,
            count,
            width,
            height,
            width,
            height,
            random,
            blueprintCatalog);
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
        int fallbackWidth,
        int fallbackHeight,
        DeterministicRandom random,
        StructureBlueprintCatalog blueprintCatalog)
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

        int placed = PlaceManyFromCandidates(
            plan,
            config,
            visitorCell,
            sorted,
            type,
            role,
            count,
            width,
            height,
            blueprintCatalog);

        if (placed >= count)
        {
            return;
        }

        List<WorldCoord> fallbackCandidates = sorted
            .Concat(PickRoadAdjacentFallback(plan, count - placed, (ulong)(type.GetHashCode() + placed + 17)))
            .Distinct()
            .ToList();

        PlaceManyFromCandidates(
            plan,
            config,
            visitorCell,
            fallbackCandidates,
            type,
            role,
            count - placed,
            fallbackWidth,
            fallbackHeight,
            blueprintCatalog);
    }

    private static int PlaceManyFromCandidates(
        IslandPlan plan,
        IslandDefinition config,
        WorldCoord visitorCell,
        IEnumerable<WorldCoord> candidates,
        StructureType type,
        IslandCellRole role,
        int count,
        int width,
        int height,
        StructureBlueprintCatalog blueprintCatalog)
    {
        int placed = 0;
        foreach (WorldCoord cell in candidates)
        {
            if (placed >= count)
            {
                break;
            }

            if (cell == visitorCell && (width > LocalMap.Width || height > LocalMap.Height))
            {
                continue;
            }

            if (plan.GetCell(cell).Role is not (IslandCellRole.None or IslandCellRole.Coast))
            {
                continue;
            }

            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, width, height);
            if (!IslandPlacementHelper.CanPlaceFootprint(plan, gx, gy, width, height))
            {
                continue;
            }

            IslandPlacementHelper.MarkFootprintRoles(plan, gx, gy, width, height, role);
            StructurePlacement structure = StructurePlacement.CreatePending(type, gx, gy, width, height);
            plan.Structures.Add(structure);
            AddStructureDoorSpur(plan, structure, config, blueprintCatalog);
            placed++;
        }

        return placed;
    }

    private static void AddStructureDoorSpur(
        IslandPlan plan,
        StructurePlacement structure,
        IslandDefinition config,
        StructureBlueprintCatalog blueprintCatalog)
    {
        StructureBlueprintDefinition blueprint = blueprintCatalog.Resolve(structure.Type);
        WorldCoord doorCell = StructurePlacementQueries.DoorCell(structure, blueprint);
        RoadNetworkStage.AddStructureSpur(plan, doorCell, config);
    }
}
