using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;
using Game.Simulation.World.Island;

namespace Game.Generation.Passes;

public sealed class FenceStampPass : IGenerationPass
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

        foreach (FenceRing fence in context.IslandPlan.FenceRings)
        {
            for (int angle = 0; angle < 360; angle++)
            {
                float radians = angle * MathF.PI / 180f;
                int gx = fence.GlobalCenterX + (int)MathF.Round(MathF.Cos(radians) * fence.Radius);
                int gy = fence.GlobalCenterY + (int)MathF.Round(MathF.Sin(radians) * fence.Radius);

                if (gx < cellMinX || gx > cellMaxX || gy < cellMinY || gy > cellMaxY)
                {
                    continue;
                }

                if (gx == fence.GateGlobalX && gy == fence.GateGlobalY)
                {
                    continue;
                }

                int localX = gx - cellMinX;
                int localY = gy - cellMinY;
                map.SetTerrain(localX, localY, TerrainId.Fence, TileFlags.BlocksMovement | TileFlags.BlocksVision);
            }

            if (fence.GateGlobalX >= cellMinX && fence.GateGlobalX <= cellMaxX &&
                fence.GateGlobalY >= cellMinY && fence.GateGlobalY <= cellMaxY)
            {
                int localX = fence.GateGlobalX - cellMinX;
                int localY = fence.GateGlobalY - cellMinY;
                map.SetTerrain(localX, localY, TerrainId.Door, TileFlags.None);
            }
        }
    }
}
