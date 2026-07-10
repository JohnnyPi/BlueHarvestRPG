using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class RiverStampPass : IGenerationPass
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

        foreach ((int globalX, int globalY) in context.IslandPlan.RiverGraph.GlobalRiverTiles)
        {
            if (globalX < cellMinX || globalX > cellMaxX || globalY < cellMinY || globalY > cellMaxY)
            {
                continue;
            }

            int localX = globalX - cellMinX;
            int localY = globalY - cellMinY;
            StampRiver(map, localX, localY);
        }
    }

    private static void StampRiver(LocalMap map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= LocalMap.Width || y >= LocalMap.Height)
        {
            return;
        }

        int index = map.GetIndex(x, y);
        TerrainId terrain = map.Terrain[index];
        if (terrain is TerrainId.Road or TerrainId.Door or TerrainId.Wall or TerrainId.Concrete
            or TerrainId.InteriorWall or TerrainId.Fence or TerrainId.Floor)
        {
            if (terrain == TerrainId.Road)
            {
                map.SetTerrain(x, y, TerrainId.ShallowFord, TileFlags.ContainsWater);
            }

            return;
        }

        map.SetTerrain(x, y, TerrainId.ShallowFord, TileFlags.ContainsWater);
    }
}
