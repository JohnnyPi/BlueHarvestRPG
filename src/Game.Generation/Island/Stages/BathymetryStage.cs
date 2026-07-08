using Game.Content.Definitions;
using Game.Generation.Noise;
using Game.Simulation.Seeds;
using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island.Stages;

public static class BathymetryStage
{
    private const uint StageSalt = 16;

    public static void Execute(IslandPlan plan, IslandDefinition config, ulong seed)
    {
        if (config.UseLegacyIslandMask)
        {
            return;
        }

        ulong stageSeed = SeedUtility.DeriveStage(seed, StageSalt);

        for (int y = 0; y < plan.Height; y++)
        {
            for (int x = 0; x < plan.Width; x++)
            {
                int index = y * plan.Width + x;
                ref IslandCellData cell = ref plan.GetCell(x, y);
                if (cell.IsLand)
                {
                    continue;
                }

                float coastDistance = plan.CoastDistance.Length > index ? plan.CoastDistance[index] : 0f;
                if (coastDistance > 0f)
                {
                    cell.Elevation = -config.ShelfDepth * 0.35f;
                    cell.Biome = coastDistance <= config.BeachCoastDistance
                        ? BiomeId.ShallowWater
                        : BiomeId.Ocean;
                    continue;
                }

                float offshore = -coastDistance;
                float px = x / (float)Math.Max(1, plan.Width - 1) * 2f - 1f;
                float concavity = plan.Concavity.Length > index ? plan.Concavity[index] : 0f;
                float eastBias = NoiseUtility.SmoothStep(-0.2f, 0.8f, px);
                float shelfWidth = config.ShelfWidth
                    + NoiseUtility.Fbm(stageSeed, px * 2f, y / (float)Math.Max(1, plan.Height - 1) * 2f, octaves: 2) * 0.04f
                    + concavity * 0.06f
                    + eastBias * 0.03f;

                float shelfDepth = config.ShelfDepth * NoiseUtility.SmoothStep(0f, shelfWidth, offshore);
                float deepDepth = config.DeepOceanDepth * NoiseUtility.SmoothStep(shelfWidth, config.DeepOceanWidth, offshore);
                cell.Elevation = -(shelfDepth + deepDepth);

                cell.Biome = ClassifyOceanBiome(offshore, shelfWidth, concavity, eastBias, cell.Temperature);
            }
        }
    }

    private static BiomeId ClassifyOceanBiome(
        float offshore,
        float shelfWidth,
        float concavity,
        float eastBias,
        float temperature)
    {
        if (offshore > shelfWidth * 1.05f)
        {
            return BiomeId.Ocean;
        }

        if (offshore <= shelfWidth * 0.18f)
        {
            return BiomeId.ShallowWater;
        }

        if (concavity > 0.3f && temperature > 0.45f && offshore <= shelfWidth * 0.65f
            && (eastBias > 0.35f || concavity > 0.5f))
        {
            return BiomeId.Reef;
        }

        return BiomeId.Ocean;
    }
}
