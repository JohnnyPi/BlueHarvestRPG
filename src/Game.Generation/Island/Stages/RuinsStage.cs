using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class RuinsStage
{
    private const uint StageSalt = 8;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        List<WorldCoord> ruinCells = IslandPlacementHelper.SampleLandCells(
            plan,
            cell => !cell.IsCoast,
            config.RuinCount,
            stageSeed);

        List<WorldCoord> fortCells = IslandPlacementHelper.SampleLandCells(
            plan,
            cell => !cell.IsCoast,
            config.FortificationCount,
            stageSeed ^ 0xF0F701UL);

        foreach (WorldCoord cell in ruinCells)
        {
            if (plan.RuinSites.Count(s => s.Kind == RuinKind.AncientRuin) >= config.RuinCount)
            {
                break;
            }

            if ((plan.GetCell(cell).Role & ~IslandCellRole.Coast) != IslandCellRole.None)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Ruin);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 16, 16);
            plan.RuinSites.Add(new RuinSite(RuinKind.AncientRuin, gx, gy, 16, 16));
        }

        foreach (WorldCoord cell in fortCells)
        {
            if (plan.RuinSites.Count(s => s.Kind == RuinKind.WarFortification) >= config.FortificationCount)
            {
                break;
            }

            if ((plan.GetCell(cell).Role & ~IslandCellRole.Coast) != IslandCellRole.None)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Fortification);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 20, 14);
            plan.RuinSites.Add(new RuinSite(RuinKind.WarFortification, gx, gy, 20, 14));
        }

        EnsureMinimumSites(plan, config, stageSeed);
    }

    private static void EnsureMinimumSites(IslandPlan plan, IslandDefinition config, ulong stageSeed)
    {
        var random = new DeterministicRandom(stageSeed ^ 0xE11DEUL);
        int maxAttempts = plan.Width * plan.Height * 4;
        int attempts = 0;

        while (plan.RuinSites.Count(s => s.Kind == RuinKind.AncientRuin) < config.RuinCount)
        {
            int x = random.NextInt(plan.Width);
            int y = random.NextInt(plan.Height);
            if (!plan.Contains(x, y) || !plan.IsLand(x, y))
            {
                if (++attempts > maxAttempts)
                {
                    break;
                }

                continue;
            }

            var cell = new WorldCoord(x, y);
            if ((plan.GetCell(cell).Role & ~IslandCellRole.Coast) != IslandCellRole.None)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Ruin);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 16, 16);
            plan.RuinSites.Add(new RuinSite(RuinKind.AncientRuin, gx, gy, 16, 16));
            attempts = 0;
        }

        attempts = 0;
        while (plan.RuinSites.Count(s => s.Kind == RuinKind.WarFortification) < config.FortificationCount)
        {
            int x = random.NextInt(plan.Width);
            int y = random.NextInt(plan.Height);
            if (!plan.Contains(x, y) || !plan.IsLand(x, y))
            {
                if (++attempts > maxAttempts)
                {
                    break;
                }

                continue;
            }

            var cell = new WorldCoord(x, y);
            if ((plan.GetCell(cell).Role & ~IslandCellRole.Coast) != IslandCellRole.None)
            {
                continue;
            }

            IslandPlacementHelper.MarkRole(plan, cell, IslandCellRole.Fortification);
            (int gx, int gy) = IslandPlacementHelper.CenteredOrigin(cell, 20, 14);
            plan.RuinSites.Add(new RuinSite(RuinKind.WarFortification, gx, gy, 20, 14));
            attempts = 0;
        }
    }
}
