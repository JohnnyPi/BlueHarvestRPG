using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class TunnelStampPass : IGenerationPass
{
    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        if (context.IslandPlan is null)
        {
            return;
        }

        int cellMinX = context.WorldCoordinate.X * LocalMap.Width;
        int cellMinY = context.WorldCoordinate.Y * LocalMap.Height;
        int cellMaxX = cellMinX + LocalMap.Width - 1;
        int cellMaxY = cellMinY + LocalMap.Height - 1;

        TunnelGraph graph = context.IslandPlan.TunnelGraph;

        foreach ((int gx, int gy) in graph.AllTunnelTiles)
        {
            if (gx < cellMinX || gx > cellMaxX || gy < cellMinY || gy > cellMaxY)
            {
                continue;
            }

            int localX = gx - cellMinX;
            int localY = gy - cellMinY;

            if (graph.CavernTiles.Contains((gx, gy)))
            {
                bool edge = IsCavernEdge(graph, gx, gy);
                map.SetTerrain(
                    localX,
                    localY,
                    edge ? TerrainId.CavernWall : TerrainId.TunnelFloor,
                    edge ? TileFlags.BlocksMovement | TileFlags.BlocksVision : TileFlags.None);
            }
            else
            {
                map.SetTerrain(localX, localY, TerrainId.TunnelFloor, TileFlags.None);
            }
        }
    }

    private static bool IsCavernEdge(TunnelGraph graph, int gx, int gy)
    {
        foreach ((int dx, int dy) in new (int, int)[]
                 {
                     (1, 0), (-1, 0), (0, 1), (0, -1)
                 })
        {
            if (!graph.CavernTiles.Contains((gx + dx, gy + dy)))
            {
                return true;
            }
        }

        return false;
    }
}
