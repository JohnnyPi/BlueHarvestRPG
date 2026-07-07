using Game.Generation.Noise;
using Game.Generation.Passes;
using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

using Game.Simulation.World;

namespace Game.Generation.Passes;

public sealed class StructureGenerationPass : IGenerationPass
{
    public void Execute(LocalMap map, LocalGenerationContext context)
    {
        if (context.WorldCell.Biome is not (BiomeId.Plains or BiomeId.Forest))
        {
            return;
        }

        var random = new DeterministicRandom(context.Seed ^ 0x537452755F001UL);
        int centerX = LocalMap.Width / 2 + random.NextInt(17) - 8;
        int centerY = LocalMap.Height / 2 + random.NextInt(17) - 8;
        int radius = 3 + random.NextInt(3);

        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                var coord = new LocalCoord(x, y);
                if (!map.Contains(coord))
                {
                    continue;
                }

                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                if (Math.Abs(dx) == radius || Math.Abs(dy) == radius)
                {
                    map.SetTerrain(x, y, TerrainId.Rock, TileFlags.BlocksMovement | TileFlags.BlocksVision);
                }
                else
                {
                    map.SetTerrain(x, y, TerrainId.Dirt, TileFlags.None);
                }
            }
        }
    }
}
