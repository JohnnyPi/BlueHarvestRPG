using Game.Simulation.World;
using Game.Simulation.World.Island;

namespace Game.Generation.Island;

public static class BiomeSuitabilityHelper
{
    public static float GetClimateAffinity(BiomeId biome, IslandCellData cell)
    {
        return biome switch
        {
            BiomeId.Swamp => cell.Moisture - 0.5f,
            BiomeId.Jungle => cell.Moisture + cell.Temperature - 1f,
            BiomeId.Plains => 0.5f - cell.Moisture,
            BiomeId.Forest => cell.Moisture - 0.35f,
            BiomeId.Hills => cell.Elevation - 0.5f,
            BiomeId.Mountains => cell.Elevation - 0.7f,
            BiomeId.Volcanic => cell.VolcanicActivity,
            BiomeId.Beach => 0.2f,
            _ => 0f,
        };
    }

    public static float GetElevationMismatchPenalty(BiomeId biome, IslandCellData cell)
    {
        return biome switch
        {
            BiomeId.Mountains => MathF.Max(0f, 0.75f - cell.Elevation) * 2f,
            BiomeId.Hills => MathF.Max(0f, 0.55f - cell.Elevation) * 1.5f,
            BiomeId.Swamp or BiomeId.Jungle => MathF.Max(0f, cell.Elevation - 0.65f) * 2f,
            BiomeId.Plains => MathF.Max(0f, cell.Elevation - 0.6f),
            _ => 0f,
        };
    }

    public static float GetMoistureMismatchPenalty(BiomeId biome, IslandCellData cell)
    {
        return biome switch
        {
            BiomeId.Swamp or BiomeId.Jungle => MathF.Max(0f, 0.5f - cell.Moisture) * 2f,
            BiomeId.Plains => MathF.Max(0f, cell.Moisture - 0.55f),
            BiomeId.Forest => MathF.Max(0f, 0.35f - cell.Moisture),
            _ => 0f,
        };
    }

    public static float ScoreBiomeForCell(
        BiomeId biome,
        IslandCellData cell,
        float regionThemeBonus = 0f,
        float riverBonus = 0f)
    {
        return GetClimateAffinity(biome, cell) * 2f
            + regionThemeBonus
            + riverBonus
            - GetElevationMismatchPenalty(biome, cell)
            - GetMoistureMismatchPenalty(biome, cell);
    }

    public static float GetRiverSuitabilityBonus(BiomeId biome, float riverInfluence)
    {
        if (riverInfluence <= 0.01f)
        {
            return 0f;
        }

        return biome switch
        {
            BiomeId.Swamp => riverInfluence * 1.5f,
            BiomeId.Jungle or BiomeId.Forest => riverInfluence * 1.2f,
            BiomeId.Plains => riverInfluence * 0.4f,
            BiomeId.Mountains => -riverInfluence * 0.8f,
            _ => 0f,
        };
    }
}
