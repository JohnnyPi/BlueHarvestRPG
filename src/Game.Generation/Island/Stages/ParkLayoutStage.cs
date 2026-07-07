using Game.Content.Definitions;
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

        WorldCoord? visitorCell = IslandPlacementHelper.FindCentralMainIslandCell(plan);
        if (visitorCell is null)
        {
            return;
        }

        plan.VisitorCenterCell = visitorCell.Value;
        plan.VisitorCenterRegionId = plan.GetRegionId(visitorCell.Value.X, visitorCell.Value.Y);
        IslandPlacementHelper.MarkRole(plan, visitorCell.Value, IslandCellRole.VisitorCenter);

        (int visitorGx, int visitorGy) = IslandPlacementHelper.CenteredOrigin(visitorCell.Value, 28, 24);
        plan.Structures.Add(new StructurePlacement(
            StructureType.VisitorCenter,
            visitorGx,
            visitorGy,
            28,
            24));

        List<WorldCoord> coastCells = IslandPlacementHelper.FindCoastalCells(plan);
        List<WorldCoord> dockCells = IslandPlacementHelper.PickSpreadCells(
            coastCells,
            config.DockCount,
            stageSeed ^ 0xD0C001UL);

        foreach (WorldCoord dockCell in dockCells)
        {
            IslandPlacementHelper.MarkRole(plan, dockCell, IslandCellRole.Dock);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(dockCell, 20, 12);
            plan.Structures.Add(new StructurePlacement(StructureType.Dock, gx, gy, 20, 12));
        }

        List<WorldCoord> visitorNeighbors = IslandPlacementHelper
            .FindLandCellsInRegion(plan, plan.VisitorCenterRegionId)
            .Where(c => c != visitorCell.Value)
            .OrderBy(_ => random.NextFloat())
            .ToList();

        PlaceNearVisitor(plan, visitorCell.Value, visitorNeighbors, StructureType.Helipad, IslandCellRole.Helipad, config.HelipadCount, 10, 10, random);
        PlaceNearVisitor(plan, visitorCell.Value, visitorNeighbors, StructureType.Hotel, IslandCellRole.Hotel, config.HotelCount, 24, 18, random);
        PlaceNearVisitor(plan, visitorCell.Value, visitorNeighbors, StructureType.Restaurant, IslandCellRole.Restaurant, config.RestaurantCount, 14, 12, random);
        PlaceNearVisitor(plan, visitorCell.Value, visitorNeighbors, StructureType.Attraction, IslandCellRole.Attraction, config.AttractionCount, 18, 18, random);

        if (!plan.Structures.Any(s => s.Type == StructureType.Attraction))
        {
            List<WorldCoord> attractionCells = IslandPlacementHelper.SampleLandCells(
                plan,
                cell => cell.Role == IslandCellRole.None,
                config.AttractionCount,
                stageSeed ^ 0xA77AC701UL);

            foreach (WorldCoord cell in attractionCells)
            {
                IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Attraction);
                (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 18, 18);
                plan.Structures.Add(new StructurePlacement(StructureType.Attraction, gx, gy, 18, 18));
            }
        }
    }

    private static void PlaceNearVisitor(
        IslandPlan plan,
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
            plan.Structures.Add(new StructurePlacement(type, gx, gy, width, height));
            placed++;
        }
    }
}
