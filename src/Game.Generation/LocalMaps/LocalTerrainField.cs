using Game.Generation.Noise;

namespace Game.Generation.LocalMaps;

public sealed class LocalTerrainField
{
    private readonly ulong _seed;

    public LocalTerrainField(ulong worldSeed)
    {
        _seed = worldSeed;
    }

    public float SampleDensity(int globalX, int globalY, int channel = 0)
    {
        return NoiseUtility.Fbm(
            _seed + (ulong)channel * 17,
            globalX * 0.07f,
            globalY * 0.07f,
            octaves: 3);
    }

    public float SampleAccent(int globalX, int globalY)
    {
        return NoiseUtility.Fbm(
            _seed + 91,
            globalX * 0.14f,
            globalY * 0.14f,
            octaves: 2);
    }

    public static (int GlobalX, int GlobalY) ToGlobalTile(int worldCellX, int worldCellY, int localX, int localY)
    {
        return (
            worldCellX * Game.Simulation.LocalMaps.LocalMap.Width + localX,
            worldCellY * Game.Simulation.LocalMaps.LocalMap.Height + localY);
    }
}
