using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.Perception;

public sealed class NoiseField
{
    public byte[] Strength { get; }
    public long[] SourceTurn { get; }

    public NoiseField()
    {
        int size = LocalMap.Width * LocalMap.Height;
        Strength = new byte[size];
        SourceTurn = new long[size];
    }

    public void Emit(LocalMap map, LocalCoord position, byte strength, long worldTime)
    {
        if (!map.Contains(position))
        {
            return;
        }

        int index = map.GetIndex(position.X, position.Y);
        if (strength > Strength[index])
        {
            Strength[index] = strength;
            SourceTurn[index] = worldTime;
        }
    }

    public void Decay(long worldTime, long maxAge = 8)
    {
        for (int i = 0; i < Strength.Length; i++)
        {
            if (Strength[i] == 0)
            {
                continue;
            }

            if (worldTime - SourceTurn[i] > maxAge)
            {
                Strength[i] = 0;
                SourceTurn[i] = 0;
            }
            else if (Strength[i] > 0)
            {
                Strength[i]--;
            }
        }
    }

    public byte GetStrength(LocalMap map, LocalCoord position)
    {
        if (!map.Contains(position))
        {
            return 0;
        }

        return Strength[map.GetIndex(position.X, position.Y)];
    }
}
