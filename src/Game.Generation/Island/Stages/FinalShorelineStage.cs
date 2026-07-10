using Game.Content.Definitions;
using Game.Generation.Island.Fields;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class FinalShorelineStage
{
    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        int count = plan.Width * plan.Height;
        var exterior = new bool[count];
        var queue = new Queue<int>();

        for (int x = 0; x < plan.Width; x++)
        {
            EnqueueWater(plan, exterior, queue, x, 0);
            EnqueueWater(plan, exterior, queue, x, plan.Height - 1);
        }

        for (int y = 1; y < plan.Height - 1; y++)
        {
            EnqueueWater(plan, exterior, queue, 0, y);
            EnqueueWater(plan, exterior, queue, plan.Width - 1, y);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % plan.Width;
            int y = index / plan.Width;
            foreach ((int dx, int dy) in Neighbors)
            {
                EnqueueWater(plan, exterior, queue, x + dx, y + dy);
            }
        }

        plan.ExteriorOcean = exterior;
        for (int i = 0; i < count; i++)
        {
            ref IslandCellData cell = ref plan.Cells[i];
            cell.IsCoast = false;
            cell.Role &= ~IslandCellRole.Coast;
            if (cell.IsLand)
            {
                continue;
            }

            cell.Biome = exterior[i] ? BiomeId.Ocean : BiomeId.ShallowWater;
        }

        CoastDistanceField.ComputeFromExteriorOcean(plan, exterior, maxDistanceNorm: 0.5f);
        LandmassStage.MarkCoastline(plan, config);
    }

    private static void EnqueueWater(
        IslandPlan plan,
        bool[] exterior,
        Queue<int> queue,
        int x,
        int y)
    {
        if (!plan.Contains(x, y) || plan.IsLand(x, y))
        {
            return;
        }

        int index = y * plan.Width + x;
        if (exterior[index])
        {
            return;
        }

        exterior[index] = true;
        queue.Enqueue(index);
    }
}
