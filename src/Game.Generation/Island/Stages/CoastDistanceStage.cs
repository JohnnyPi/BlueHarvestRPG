using Game.Content.Definitions;
using Game.Generation.Island.Fields;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Computes signed coast distance from the island mask.
/// Positive = inland, negative = ocean, zero = shoreline.
/// </summary>
public static class CoastDistanceStage
{
    public static void Execute(IslandPlan plan, IslandDefinition config)
    {
        float landThreshold = config.UseLegacyIslandMask
            ? config.LandElevationThreshold * 0.5f
            : config.IslandShape.LandThreshold;

        CoastDistanceField.Compute(plan, landThreshold, maxDistanceNorm: 0.5f);
    }
}
