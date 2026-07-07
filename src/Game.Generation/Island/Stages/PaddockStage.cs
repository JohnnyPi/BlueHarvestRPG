using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class PaddockStage
{
    private const uint StageSalt = 5;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        var candidates = new List<WorldCoord>();

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (!cell.IsLand || cell.IsCoast)
                {
                    continue;
                }

                if (cell.Role != IslandCellRole.None)
                {
                    continue;
                }

                if (cell.Biome is not (BiomeId.Forest or BiomeId.Jungle or BiomeId.Plains or BiomeId.Hills))
                {
                    continue;
                }

                if (plan.VisitorCenterCell.X >= 0)
                {
                    int dx = x - plan.VisitorCenterCell.X;
                    int dy = y - plan.VisitorCenterCell.Y;
                    if (dx * dx + dy * dy < 16)
                    {
                        continue;
                    }
                }

                candidates.Add(new WorldCoord(x, y));
            }
        }

        List<WorldCoord> paddockCells = IslandPlacementHelper.PickSpreadCells(
            candidates,
            config.PaddockCount,
            stageSeed);

        if (paddockCells.Count < config.PaddockCount)
        {
            paddockCells = IslandPlacementHelper.SampleLandCells(
                plan,
                cell => !cell.IsCoast && cell.Role == IslandCellRole.None,
                config.PaddockCount,
                stageSeed ^ 0xBAD0C01UL);
        }

        int paddockIndex = 0;
        foreach (WorldCoord cell in paddockCells)
        {
            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Paddock);
            (int centerGx, int centerGy) = IslandPlacementHelper.CellCenterGlobal(cell);
            int radius = config.PaddockFenceRadius;

            int gateGx = centerGx + radius;
            int gateGy = centerGy;

            plan.FenceRings.Add(new FenceRing(
                centerGx,
                centerGy,
                radius,
                gateGx,
                gateGy,
                paddockIndex++));
        }
    }
}
