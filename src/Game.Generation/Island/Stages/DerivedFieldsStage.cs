using Game.Content.Definitions;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class DerivedFieldsStage
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        int count = plan.Width * plan.Height;
        plan.Slope = new float[count];
        plan.Aspect = new float[count];
        plan.Curvature = new float[count];
        plan.WaveExposure = new float[count];

        if (plan.Drainage.Length != count)
        {
            plan.Drainage = new float[count];
        }

        if (plan.RiverInfluence.Length != count)
        {
            ComputeRiverInfluence(plan);
        }

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                if (!plan.IsLand(x, y))
                {
                    continue;
                }

                float dzdx = SampleElevation(plan, x + 1, y) - SampleElevation(plan, x - 1, y);
                float dzdy = SampleElevation(plan, x, y + 1) - SampleElevation(plan, x, y - 1);
                float slope = MathF.Sqrt(dzdx * dzdx + dzdy * dzdy) * 0.5f;
                plan.Slope[index] = Math.Clamp(slope, 0f, 1f);
                plan.Aspect[index] = MathF.Atan2(dzdy, dzdx);
                plan.Curvature[index] = ComputeLaplacian(plan, x, y);

                float coastDistance = plan.CoastDistance.Length > index ? plan.CoastDistance[index] : 0f;
                float concavity = plan.Concavity.Length > index ? plan.Concavity[index] : 0f;
                float offshore = coastDistance <= 0f ? 1f : 1f / (1f + coastDistance * 8f);
                plan.WaveExposure[index] = Math.Clamp(offshore * (1f - concavity * 0.5f), 0f, 1f);
            }
        }
    }

    public static void ComputeRiverInfluence(IslandPlan plan)
    {
        int count = plan.Width * plan.Height;
        plan.RiverInfluence = new float[count];
        var queue = new Queue<(int Index, float Distance)>();

        for (int i = 0; i < count; i++)
        {
            if (plan.IsRiverCell.Length > i && plan.IsRiverCell[i])
            {
                plan.RiverInfluence[i] = 1f;
                queue.Enqueue((i, 0f));
            }
        }

        while (queue.Count > 0)
        {
            (int index, float distance) = queue.Dequeue();
            int x = index % plan.Width;
            int y = index / plan.Width;

            foreach ((int dx, int dy) in Neighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (!plan.Contains(nx, ny))
                {
                    continue;
                }

                int neighborIndex = ny * plan.Width + nx;
                float nextDistance = distance + 1f;
                float influence = MathF.Exp(-nextDistance * 0.12f);
                if (influence <= plan.RiverInfluence[neighborIndex])
                {
                    continue;
                }

                plan.RiverInfluence[neighborIndex] = influence;
                queue.Enqueue((neighborIndex, nextDistance));
            }
        }
    }

    private static float SampleElevation(IslandPlan plan, int x, int y)
    {
        if (!plan.Contains(x, y))
        {
            return plan.GetCell(Math.Clamp(x, 0, plan.Width - 1), Math.Clamp(y, 0, plan.Height - 1)).Elevation;
        }

        return plan.GetCell(x, y).Elevation;
    }

    private static float ComputeLaplacian(IslandPlan plan, int x, int y)
    {
        float center = plan.GetCell(x, y).Elevation;
        float sum = 0f;
        int count = 0;

        foreach ((int dx, int dy) in Neighbors)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!plan.Contains(nx, ny))
            {
                continue;
            }

            sum += plan.GetCell(nx, ny).Elevation;
            count++;
        }

        if (count == 0)
        {
            return 0f;
        }

        return (sum / count) - center;
    }
}
