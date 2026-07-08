using Game.Simulation.Time;

namespace Game.Simulation.World;

public static class BiomeTraversal
{
    public const int OverworldTravelHours = 1;

    public static bool IsPassable(BiomeId biome)
    {
        return biome switch
        {
            BiomeId.Ocean => false,
            BiomeId.ShallowWater => false,
            BiomeId.Reef => false,
            BiomeId.Mountains => false,
            _ => true
        };
    }

    public static int GetMoveCost(BiomeId biome)
    {
        return biome switch
        {
            BiomeId.Ocean => int.MaxValue,
            BiomeId.ShallowWater => int.MaxValue,
            BiomeId.Reef => int.MaxValue,
            BiomeId.Beach => 30,
            BiomeId.Plains => 40,
            BiomeId.Forest => 50,
            BiomeId.Jungle => 60,
            BiomeId.Swamp => 70,
            BiomeId.Hills => 55,
            BiomeId.Mountains => 500,
            BiomeId.Volcanic => 65,
            _ => ActionCostTable.Walk
        };
    }
}
