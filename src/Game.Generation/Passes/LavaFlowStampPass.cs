using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class LavaFlowStampPass : IGenerationPass
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

        foreach ((int globalX, int globalY) in context.IslandPlan.LavaFlowGraph.GlobalLavaTiles)
        {
            if (globalX < cellMinX || globalX > cellMaxX || globalY < cellMinY || globalY > cellMaxY)
            {
                continue;
            }

            StampLava(map, globalX - cellMinX, globalY - cellMinY);
        }
    }

    private static void StampLava(LocalMap map, int x, int y)
    {
        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain is TerrainId.DeepWater or TerrainId.ShallowWater or TerrainId.ShallowFord
            or TerrainId.Road or TerrainId.Concrete or TerrainId.Wall or TerrainId.Fence
            or TerrainId.Floor or TerrainId.Door or TerrainId.Dock or TerrainId.InteriorWall
            or TerrainId.StructureExit)
        {
            return;
        }

        map.SetTerrain(x, y, TerrainId.Lava, TileFlags.BlocksMovement);
    }
}
