using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Simulation.Scenarios;

public static class ScenarioGenerator
{
    private static readonly string[] Missions =
    [
        "Rescue stranded tourists",
        "Retrieve corporate assets",
        "Evacuate research staff",
        "Investigate silent outpost",
        "Recover black-box flight data"
    ];

    private static readonly string[] StartLocations =
    [
        "Abandoned dormitories",
        "Storm-damaged visitor center",
        "Maintenance bunkers",
        "Beach evacuation camp",
        "Overgrown service road"
    ];

    private static readonly string[] EscapeRoutes =
    [
        "Offshore yacht",
        "Emergency heli-pad",
        "Smuggler cove",
        "Repaired monorail spur",
        "Submersible dock"
    ];

    private static readonly string[] Obstacles =
    [
        "Monorail is offline",
        "Raptor territory blocks the maintenance route",
        "Radio tower jamming all channels",
        "Volcanic vents closed the east road",
        "Flooded tunnels under the hotel district",
        "Power grid failure sealed the blast doors",
        "A sick alpha blocks the north ridge"
    ];

    private static readonly string[] Mysteries =
    [
        "Why are radio signals being overpowered?",
        "Who disabled the perimeter fences?",
        "What is the old lab really growing?",
        "Why did the evacuation fleet never return?",
        "What is broadcasting from the sea caves?"
    ];

    private static readonly string[] Encounters =
    [
        "Sick triceratops acting strangely",
        "Raptor pack testing a weakened fence",
        "Stampeding hadrosaurs crossing a bridge",
        "Dilophosaur pair stalking the marsh lights",
        "Ankylosaur blocking a supply tunnel"
    ];

    private static readonly string[] Secrets =
    [
        "The old lab was modifying behavior, not cloning stock",
        "The park reopened under a shell company last month",
        "A rival expedition triggered the containment breach",
        "The island sits on a temporal fracture",
        "Corporate brass planned the 'accident' for the insurance payout"
    ];

    public static RunScenario Generate(ulong seed, IslandPlan? plan = null)
    {
        ulong state = SeedUtility.DeriveStage(seed, 91);

        string obstacle1 = Pick(ref state, Obstacles);

        var scenario = new RunScenario
        {
            Mission = Pick(ref state, Missions),
            StartLocation = Pick(ref state, StartLocations),
            EscapeRoute = Pick(ref state, EscapeRoutes),
            Obstacle1 = obstacle1,
            Obstacle2 = PickDistinct(ref state, Obstacles, obstacle1),
            Mystery = Pick(ref state, Mysteries),
            FirstEncounter = Pick(ref state, Encounters),
            IslandSecret = Pick(ref state, Secrets)
        };

        if (plan is not null)
        {
            ScenarioObjectiveBinder.Bind(scenario, plan);
            ScenarioObstacleBinder.Bind(scenario, plan);
        }

        return scenario;
    }

    private static string PickDistinct(ref ulong state, string[] options, string obstacle1)
    {
        if (options.Length <= 1)
        {
            return options[0];
        }

        string picked = Pick(ref state, options);
        for (int attempt = 0; attempt < 8 && picked == obstacle1; attempt++)
        {
            picked = Pick(ref state, options);
        }

        return picked == obstacle1
            ? options.First(option => option != obstacle1)
            : picked;
    }

    private static string Pick(ref ulong state, string[] options)
    {
        state = Mix(state);
        int index = (int)(state % (ulong)options.Length);
        return options[index];
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 12;
            value *= 0x2545F4914F6CDD1DUL;
            value ^= value >> 32;
            value *= 0x5851F42D4C957F2DUL;
            value ^= value >> 29;
            return value;
        }
    }
}
