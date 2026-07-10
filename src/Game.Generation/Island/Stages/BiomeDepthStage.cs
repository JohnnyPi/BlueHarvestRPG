using Game.Content.Definitions;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class BiomeDepthStage
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        int count = plan.Width * plan.Height;
        plan.BiomeDepth = new float[count];
        var rawDepth = new int[count];
        Array.Fill(rawDepth, -1);

        var queue = new Queue<int>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                BiomeId biome = plan.GetCell(x, y).Biome;
                bool isEdge = false;

                foreach ((int dx, int dy) in Neighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (!plan.Contains(nx, ny) || !plan.IsLand(nx, ny) ||
                        plan.GetCell(nx, ny).Biome != biome)
                    {
                        isEdge = true;
                        break;
                    }
                }

                if (isEdge)
                {
                    rawDepth[index] = 0;
                    queue.Enqueue(index);
                }
            }
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % plan.Width;
            int y = index / plan.Width;
            BiomeId biome = plan.GetCell(x, y).Biome;
            int nextDepth = rawDepth[index] + 1;

            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.Contains(nx, ny) || !plan.IsLand(nx, ny))
                {
                    continue;
                }

                int neighborIndex = ny * plan.Width + nx;
                if (plan.GetCell(nx, ny).Biome != biome || rawDepth[neighborIndex] >= 0)
                {
                    continue;
                }

                rawDepth[neighborIndex] = nextDepth;
                queue.Enqueue(neighborIndex);
            }
        }

        const float depthScale = 12f;
        for (int i = 0; i < count; i++)
        {
            if (rawDepth[i] < 0)
            {
                plan.BiomeDepth[i] = 0f;
                continue;
            }

            plan.BiomeDepth[i] = MathF.Min(1f, rawDepth[i] / depthScale);
        }
    }
}
