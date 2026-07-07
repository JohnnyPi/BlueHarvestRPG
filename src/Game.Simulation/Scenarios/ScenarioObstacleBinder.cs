using Game.Simulation.Coordinates;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Simulation.Scenarios;

public static class ScenarioObstacleBinder
{
    public static void Bind(RunScenario scenario, IslandPlan plan)
    {
        ulong seed = plan.Seed;

        scenario.Obstacle1Target = ResolveObstacleCell(plan, scenario.Obstacle1, seed, ordinal: 0)
            ?? ScenarioCellPicker.PickDistinctCell(
                plan,
                seed,
                ordinal: 0,
                scenario.EscapeTarget,
                scenario.MysteryTarget);

        scenario.Obstacle2Target = ResolveObstacleCell(plan, scenario.Obstacle2, seed, ordinal: 1)
            ?? ScenarioCellPicker.PickDistinctCell(
                plan,
                seed,
                ordinal: 1,
                scenario.EscapeTarget,
                scenario.MysteryTarget,
                scenario.Obstacle1Target);
    }

    private static WorldCoord? ResolveObstacleCell(IslandPlan plan, string obstacle, ulong seed, int ordinal)
    {
        IslandCellRole role = MapObstacleRole(obstacle);
        WorldCoord? byRole = ScenarioCellPicker.FindCellWithRole(plan, role, seed, ordinal + 10);
        if (byRole is not null)
        {
            return byRole;
        }

        BiomeId? biome = MapObstacleBiome(obstacle);
        if (biome is BiomeId biomeId)
        {
            return ScenarioCellPicker.FindCellWithBiome(plan, biomeId, seed, ordinal + 10);
        }

        return null;
    }

    private static IslandCellRole MapObstacleRole(string obstacle)
    {
        return obstacle switch
        {
            "Monorail is offline" => IslandCellRole.Maintenance,
            "Raptor territory blocks the maintenance route" => IslandCellRole.Paddock,
            "Radio tower jamming all channels" => IslandCellRole.Maintenance,
            "Flooded tunnels under the hotel district" => IslandCellRole.Tunnel,
            "Power grid failure sealed the blast doors" => IslandCellRole.Fortification,
            _ => IslandCellRole.Maintenance
        };
    }

    private static BiomeId? MapObstacleBiome(string obstacle)
    {
        return obstacle switch
        {
            "Volcanic vents closed the east road" => BiomeId.Volcanic,
            "A sick alpha blocks the north ridge" => BiomeId.Mountains,
            _ => null
        };
    }
}
