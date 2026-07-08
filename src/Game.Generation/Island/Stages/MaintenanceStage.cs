using Game.Content.Definitions;
using Game.Generation.Island.Stages;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class MaintenanceStage
{
    private const uint StageSalt = 6;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        var candidates = new List<WorldCoord>();

        foreach (FenceRing fence in plan.FenceRings)
        {
            (WorldCoord world, _) = CoordinateMath.FromGlobalTile(new GlobalTileCoord(fence.GlobalCenterX, fence.GlobalCenterY));

            foreach ((int dx, int dy) in new (int, int)[]
                     {
                         (1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (0, 2)
                     })
            {
                int x = world.X + dx;
                int y = world.Y + dy;

                if (!plan.Contains(x, y) || !plan.IsLand(x, y))
                {
                    continue;
                }

                var coord = new WorldCoord(x, y);
                if (plan.GetCell(coord).Role != IslandCellRole.None)
                {
                    continue;
                }

                if (!plan.RoadGraph.IsAdjacentToRoad(coord))
                {
                    continue;
                }

                if (!candidates.Contains(coord))
                {
                    candidates.Add(coord);
                }
            }
        }

        if (plan.VisitorCenterCell.X >= 0)
        {
            foreach ((int dx, int dy) in new (int, int)[] { (1, 1), (-1, 1), (1, -1) })
            {
                int x = plan.VisitorCenterCell.X + dx;
                int y = plan.VisitorCenterCell.Y + dy;
                if (plan.Contains(x, y) && plan.IsLand(x, y))
                {
                    var coord = new WorldCoord(x, y);
                    if (plan.GetCell(coord).Role == IslandCellRole.None &&
                        plan.RoadGraph.IsAdjacentToRoad(coord))
                    {
                        candidates.Add(coord);
                    }
                }
            }
        }

        List<WorldCoord> maintenanceCells = IslandPlacementHelper.PickSpreadCells(
            candidates,
            config.MaintenanceAreaCount,
            stageSeed);

        if (maintenanceCells.Count < config.MaintenanceAreaCount)
        {
            var roadAdjacent = new List<WorldCoord>();
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
                        roadAdjacent.Add(coord);
                    }
                }
            }

            maintenanceCells = IslandPlacementHelper.PickSpreadCells(
                roadAdjacent,
                config.MaintenanceAreaCount,
                stageSeed ^ 0xA11CEUL);

            if (maintenanceCells.Count < config.MaintenanceAreaCount)
            {
                maintenanceCells = IslandPlacementHelper.SampleLandCells(
                    plan,
                    cell => cell.Role is IslandCellRole.None or IslandCellRole.Coast,
                    config.MaintenanceAreaCount,
                    stageSeed ^ 0xA11CEUL);
            }
        }

        foreach (WorldCoord cell in maintenanceCells)
        {
            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Maintenance);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 12, 10);
            plan.Structures.Add(StructurePlacement.CreatePending(
                StructureType.MaintenanceCompound,
                gx,
                gy,
                12,
                10));
            RoadNetworkStage.AddStructureSpur(plan, cell, config);
        }
    }
}
