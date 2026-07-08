using Game.Simulation.World.Island;

namespace Game.Generation.Island.Fields;

public static class CoastDistanceField
{
    private static readonly (int Dx, int Dy)[] Neighbors =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (-1, 1), (1, -1), (-1, -1)
    ];

    public static void Compute(IslandPlan plan, float landThreshold, float maxDistanceNorm)
    {
        int cellCount = plan.Width * plan.Height;
        plan.CoastDistance = new float[cellCount];
        plan.Concavity = new float[cellCount];

        var land = new bool[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            land[i] = plan.IslandMask[i] > landThreshold;
        }

        float maxDistCells = MathF.Max(1f, maxDistanceNorm * Math.Min(plan.Width, plan.Height));
        var dist = new float[cellCount];
        Array.Fill(dist, float.MaxValue);

        var queue = new Queue<int>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (!IsCoastCell(land, plan.Width, plan.Height, x, y))
                {
                    continue;
                }

                dist[index] = 0f;
                queue.Enqueue(index);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            int cx = current % plan.Width;
            int cy = current / plan.Width;
            float currentDist = dist[current];

            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = cx + dx;
                int ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= plan.Width || ny >= plan.Height)
                {
                    continue;
                }

                int neighbor = ny * plan.Width + nx;
                float step = dx != 0 && dy != 0 ? 1.4142135f : 1f;
                float candidate = currentDist + step;
                if (candidate >= dist[neighbor])
                {
                    continue;
                }

                dist[neighbor] = candidate;
                queue.Enqueue(neighbor);
            }
        }

        for (int i = 0; i < cellCount; i++)
        {
            float normalized = dist[i] == float.MaxValue
                ? maxDistanceNorm
                : dist[i] / maxDistCells;

            plan.CoastDistance[i] = land[i] ? normalized : -normalized;
        }

        ComputeConcavity(plan, land, maxDistCells);
    }

    private static bool IsCoastCell(bool[] land, int width, int height, int x, int y)
    {
        int index = y * width + x;
        bool isLand = land[index];

        foreach ((int dx, int dy) in Neighbors)
        {
            if (dx != 0 && dy != 0)
            {
                continue;
            }

            int nx = x + dx;
            int ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
            {
                return true;
            }

            if (land[ny * width + nx] != isLand)
            {
                return true;
            }
        }

        return false;
    }

    private static void ComputeConcavity(IslandPlan plan, bool[] land, float maxDistCells)
    {
        int width = plan.Width;
        int height = plan.Height;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (land[index])
                {
                    plan.Concavity[index] = 0f;
                    continue;
                }

                float laplacian = 0f;
                float center = plan.CoastDistance[index];
                laplacian += plan.CoastDistance[index - 1] - center;
                laplacian += plan.CoastDistance[index + 1] - center;
                laplacian += plan.CoastDistance[index - width] - center;
                laplacian += plan.CoastDistance[index + width] - center;

                float concavity = Math.Clamp(-laplacian * maxDistCells * 0.25f, 0f, 1f);
                plan.Concavity[index] = concavity;
            }
        }
    }
}
