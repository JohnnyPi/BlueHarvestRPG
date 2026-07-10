using Game.Simulation.Coordinates;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Simulation.Scenarios;

public static class ScenarioObjectiveBinder
{
    public static void Bind(RunScenario scenario, IslandPlan plan)
    {
        ulong seed = plan.Seed;

        IslandCellRole escapeRole = MapEscapeRoute(scenario.EscapeRoute);
        scenario.EscapeTarget = ScenarioCellPicker.FindCellWithRole(plan, escapeRole, seed, 0)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.Dock, seed, 0)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.VisitorCenter, seed, 0);

        IslandCellRole mysteryRole = MapMysteryRole(scenario.Mystery, seed);
        scenario.MysteryTarget = ScenarioCellPicker.FindCellWithRole(plan, mysteryRole, seed, 1)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.Maintenance, seed, 0)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.Attraction, seed, 0);

        if (scenario.EscapeTarget is WorldCoord escapeTarget)
        {
            scenario.EscapeLandmark = OverworldLandmarkCatalog.GetName(plan, escapeTarget.X, escapeTarget.Y);
            if (string.IsNullOrEmpty(scenario.EscapeLandmark))
            {
                scenario.EscapeLandmark = scenario.EscapeRoute;
            }
        }
        else
        {
            scenario.EscapeLandmark = scenario.EscapeRoute;
        }

        if (scenario.MysteryTarget is WorldCoord mysteryTarget)
        {
            scenario.MysteryLandmark = OverworldLandmarkCatalog.GetName(plan, mysteryTarget.X, mysteryTarget.Y);
            if (string.IsNullOrEmpty(scenario.MysteryLandmark))
            {
                scenario.MysteryLandmark = "Investigation site";
            }
        }
        else
        {
            scenario.MysteryLandmark = "Investigation site";
        }
    }

    public static WorldCoord? ResolveStartCell(IslandPlan plan, string startLocation, ulong seed)
    {
        IslandCellRole role = MapStartLocation(startLocation);
        return ScenarioCellPicker.FindCellWithRole(plan, role, seed, 42)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.VisitorCenter, seed, 0)
            ?? ScenarioCellPicker.FindCellWithRole(plan, IslandCellRole.Hotel, seed, 0);
    }

    private static IslandCellRole MapStartLocation(string startLocation)
    {
        return startLocation switch
        {
            "Storm-damaged visitor center" => IslandCellRole.VisitorCenter,
            "Maintenance bunkers" => IslandCellRole.Maintenance,
            "Beach evacuation camp" => IslandCellRole.Dock,
            "Abandoned dormitories" => IslandCellRole.Hotel,
            _ => IslandCellRole.Maintenance
        };
    }

    private static IslandCellRole MapEscapeRoute(string escapeRoute)
    {
        return escapeRoute switch
        {
            "Emergency heli-pad" => IslandCellRole.Helipad,
            "Repaired monorail spur" => IslandCellRole.Maintenance,
            "Smuggler cove" => IslandCellRole.Dock,
            "Submersible dock" => IslandCellRole.Dock,
            _ => IslandCellRole.Dock
        };
    }

    private static IslandCellRole MapMysteryRole(string mystery, ulong seed)
    {
        IslandCellRole[] roles =
        [
            IslandCellRole.Maintenance,
            IslandCellRole.Ruin,
            IslandCellRole.Attraction,
            IslandCellRole.Hotel
        ];

        ulong state = SeedUtility.DeriveStage(seed, 93) ^ SeedUtility.HashString(mystery);
        int index = (int)(state % (ulong)roles.Length);
        return roles[index];
    }
}
