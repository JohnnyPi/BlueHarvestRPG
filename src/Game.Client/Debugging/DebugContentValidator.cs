using Game.Content;
using Game.Simulation.LocalMaps;
using Game.Simulation.World;

namespace Game.Client.Debugging;

public static class DebugContentValidator
{
    public static void Validate(GameContentBundle bundle)
    {
        if (!DebugMode.IsEnabled)
        {
            return;
        }

        foreach (BiomeId biome in Enum.GetValues<BiomeId>())
        {
            string key = biome.ToString();
            if (!bundle.BiomeColors.Biomes.ContainsKey(key))
            {
                DebugLog.Issue($"Missing biome color in presentation/biomes.yaml: {key}");
            }

            if (!bundle.Tiles.Biomes.ContainsKey(key))
            {
                DebugLog.Issue($"Missing biome tile in presentation/biomes.yaml: {key}");
            }
        }

        foreach (TerrainId terrain in Enum.GetValues<TerrainId>())
        {
            string key = terrain.ToString();
            if (!bundle.TerrainColors.Terrain.ContainsKey(key))
            {
                DebugLog.Issue($"Missing terrain color in presentation/terrain.yaml: {key}");
            }

            if (!bundle.Tiles.Terrain.ContainsKey(key))
            {
                DebugLog.Issue($"Missing terrain tile in presentation/tiles.yaml: {key}");
            }
        }

        DebugLog.Info(
            $"Content validated: {bundle.BiomeColors.Biomes.Count} biome colors, {bundle.TerrainColors.Terrain.Count} terrain colors, {bundle.Tiles.Terrain.Count} terrain tiles.");
    }
}
