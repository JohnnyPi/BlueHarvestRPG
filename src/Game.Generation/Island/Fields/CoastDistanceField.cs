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

        Compute(plan, land, maxDistanceNorm);
    }

    public static void ComputeFromExteriorOcean(
        IslandPlan plan,
        IReadOnlyList<bool> exteriorOcean,
        float maxDistanceNorm)
    {
        int cellCount = plan.Width * plan.Height;
        if (exteriorOcean.Count != cellCount)
        {
            throw new ArgumentException("Exterior-ocean field must match the plan dimensions.", nameof(exteriorOcean));
        }

        var landSide = new bool[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            landSide[i] = !exteriorOcean[i];
        }

        Compute(plan, landSide, maxDistanceNorm);
    }

    private static void Compute(IslandPlan plan, bool[] land, float maxDistanceNorm)
    {
        int cellCount = plan.Width * plan.Height;
        plan.CoastDistance = new float[cellCount];
        plan.Concavity = new float[cellCount];
        float maxDistCells = MathF.Max(1f, maxDistanceNorm * Math.Min(plan.Width, plan.Height));
        var dist = new float[cellCount];
        Array.Fill(dist, float.MaxValue);

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
            }
        }

        void Relax(int index, int neighbor, float step)
        {
            if (dist[neighbor] == float.MaxValue)
            {
                return;
            }

            float candidate = dist[neighbor] + step;
            if (candidate < dist[index])
            {
                dist[index] = MathF.Min(candidate, maxDistCells);
            }
        }

        const float diagonal = 1.4142135f;
        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (x > 0)
                {
                    Relax(index, index - 1, 1f);
                }

                if (y > 0)
                {
                    Relax(index, index - plan.Width, 1f);
                    if (x > 0)
                    {
                        Relax(index, index - plan.Width - 1, diagonal);
                    }

                    if (x + 1 < plan.Width)
                    {
                        Relax(index, index - plan.Width + 1, diagonal);
                    }
                }
            }
        }

        for (int y = plan.Height - 1; y >= 0; y--)
        {
            for (int x = plan.Width - 1; x >= 0; x--)
            {
                int index = y * plan.Width + x;
                if (x + 1 < plan.Width)
                {
                    Relax(index, index + 1, 1f);
                }

                if (y + 1 < plan.Height)
                {
                    Relax(index, index + plan.Width, 1f);
                    if (x > 0)
                    {
                        Relax(index, index + plan.Width - 1, diagonal);
                    }

                    if (x + 1 < plan.Width)
                    {
                        Relax(index, index + plan.Width + 1, diagonal);
                    }
                }
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
                float laplacian = 0f;
                float center = plan.CoastDistance[index];
                laplacian += plan.CoastDistance[index - 1] - center;
                laplacian += plan.CoastDistance[index + 1] - center;
                laplacian += plan.CoastDistance[index - width] - center;
                laplacian += plan.CoastDistance[index + width] - center;

                float concavity = Math.Clamp(-laplacian * maxDistCells * 0.25f, -1f, 1f);
                plan.Concavity[index] = concavity;
            }
        }
    }
}
