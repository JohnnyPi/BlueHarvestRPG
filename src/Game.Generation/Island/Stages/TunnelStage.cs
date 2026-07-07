using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class TunnelStage
{
    private const uint StageSalt = 7;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        plan.TunnelGraph.Nodes.Clear();
        plan.TunnelGraph.Segments.Clear();
        plan.TunnelGraph.AllTunnelTiles.Clear();
        plan.TunnelGraph.CavernTiles.Clear();

        if (plan.VisitorCenterCell.X < 0)
        {
            return;
        }

        (int visitorGx, int visitorGy) = IslandPlacementHelper.CellCenterGlobal(plan.VisitorCenterCell);
        int visitorNodeId = AddNode(plan, visitorGx, visitorGy, TunnelNodeKind.VisitorHub);

        var connectedPaddockNodes = new List<int>();

        foreach (FenceRing fence in plan.FenceRings)
        {
            int accessGx = fence.GateGlobalX + 2;
            int accessGy = fence.GateGlobalY;
            int nodeId = AddNode(plan, accessGx, accessGy, TunnelNodeKind.PaddockAccess);
            connectedPaddockNodes.Add(nodeId);

            var path = BuildTunnelPath(visitorGx, visitorGy, accessGx, accessGy);
            AddSegment(plan, visitorNodeId, nodeId, path);
        }

        foreach (StructurePlacement structure in plan.Structures.Where(s => s.Type == StructureType.MaintenanceCompound))
        {
            int mx = structure.GlobalOriginX + structure.Width / 2;
            int my = structure.GlobalOriginY + structure.Height / 2;
            int nodeId = AddNode(plan, mx, my, TunnelNodeKind.MaintenanceAccess);

            int anchorNode = connectedPaddockNodes.Count > 0
                ? connectedPaddockNodes[random.NextInt(connectedPaddockNodes.Count)]
                : visitorNodeId;

            TunnelNode anchor = plan.TunnelGraph.Nodes.First(n => n.Id == anchorNode);
            var path = BuildTunnelPath(anchor.GlobalX, anchor.GlobalY, mx, my);
            AddSegment(plan, anchorNode, nodeId, path);
        }

        int cavernCount = Math.Max(2, plan.FenceRings.Count / 2);
        for (int i = 0; i < cavernCount; i++)
        {
            if (plan.TunnelGraph.Segments.Count == 0)
            {
                break;
            }

            TunnelSegment segment = plan.TunnelGraph.Segments[random.NextInt(plan.TunnelGraph.Segments.Count)];
            if (segment.Path.Count == 0)
            {
                continue;
            }

            (int cx, int cy) = segment.Path[segment.Path.Count / 2];
            int cavernNodeId = AddNode(plan, cx, cy, TunnelNodeKind.Cavern);
            StampCavern(plan, cx, cy, config.TunnelCavernRadius);
            AddSegment(plan, segment.FromNodeId, cavernNodeId, [(cx, cy)]);
        }

        MarkTunnelCellsOnPlan(plan);
    }

    private static int AddNode(IslandPlan plan, int gx, int gy, TunnelNodeKind kind)
    {
        int id = plan.TunnelGraph.Nodes.Count;
        plan.TunnelGraph.Nodes.Add(new TunnelNode(id, gx, gy, kind));
        return id;
    }

    private static void AddSegment(IslandPlan plan, int fromId, int toId, List<(int X, int Y)> path)
    {
        if (path.Count == 0)
        {
            return;
        }

        plan.TunnelGraph.Segments.Add(new TunnelSegment(fromId, toId, path));

        foreach ((int x, int y) in path)
        {
            plan.TunnelGraph.AllTunnelTiles.Add((x, y));
        }
    }

    private static List<(int X, int Y)> BuildTunnelPath(int x0, int y0, int x1, int y1)
    {
        var path = new List<(int X, int Y)>();
        int x = x0;
        int y = y0;

        while (x != x1)
        {
            path.Add((x, y));
            x += x < x1 ? 1 : -1;
        }

        while (y != y1)
        {
            path.Add((x, y));
            y += y < y1 ? 1 : -1;
        }

        path.Add((x1, y1));
        return path;
    }

    private static void StampCavern(IslandPlan plan, int centerX, int centerY, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                plan.TunnelGraph.CavernTiles.Add((centerX + dx, centerY + dy));
                plan.TunnelGraph.AllTunnelTiles.Add((centerX + dx, centerY + dy));
            }
        }
    }

    private static void MarkTunnelCellsOnPlan(IslandPlan plan)
    {
        foreach ((int gx, int gy) in plan.TunnelGraph.AllTunnelTiles)
        {
            (WorldCoord world, _) = CoordinateMath.FromGlobalTile(new GlobalTileCoord(gx, gy));
            if (!plan.Contains(world.X, world.Y))
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(world);
            cell.Role |= IslandCellRole.Tunnel;
        }

        foreach ((int gx, int gy) in plan.TunnelGraph.CavernTiles)
        {
            (WorldCoord world, _) = CoordinateMath.FromGlobalTile(new GlobalTileCoord(gx, gy));
            if (!plan.Contains(world.X, world.Y))
            {
                continue;
            }

            ref IslandCellData cell = ref plan.GetCell(world);
            cell.Role |= IslandCellRole.Cavern;
        }
    }
}
