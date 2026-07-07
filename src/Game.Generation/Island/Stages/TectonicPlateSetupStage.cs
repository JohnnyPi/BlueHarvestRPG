using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

/// <summary>
/// Assigns each Voronoi region a tectonic plate identity: continental vs oceanic crust and motion vector.
/// </summary>
public static class TectonicPlateSetupStage
{
    private const uint StageSalt = 10;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);
        var random = new DeterministicRandom(stageSeed);

        float centerX = (plan.Width - 1) * 0.5f;
        float centerY = (plan.Height - 1) * 0.5f;
        float maxRadius = Math.Min(centerX, centerY);

        foreach (IslandRegion region in plan.Regions)
        {
            float dx = (region.SiteX - centerX) / maxRadius;
            float dy = (region.SiteY - centerY) / maxRadius;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            float continentalChance = config.ContinentalCrustBias;
            if (dist < config.MainIslandRadius * 0.55f)
            {
                continentalChance = Math.Clamp(continentalChance + 0.15f, 0f, 0.95f);
            }
            else if (dist > config.MainIslandRadius)
            {
                continentalChance = Math.Clamp(continentalChance - 0.2f, 0.05f, 1f);
            }

            region.IsContinental = random.NextFloat() < continentalChance;
            region.MotionAngle = random.NextFloat() * MathF.PI * 2f;
            float motionRange = Math.Max(0.01f, config.PlateMotionMax - config.PlateMotionMin);
            region.MotionMagnitude = config.PlateMotionMin + random.NextFloat() * motionRange;
        }
    }
}
