using Game.Simulation.Coordinates;
using Game.Simulation.LocalMaps;

namespace Game.Simulation.Perception;

public sealed class ScentField
{
    public byte[] Intensity { get; }

    public ScentField()
    {
        Intensity = new byte[LocalMap.Width * LocalMap.Height];
    }

    public void Deposit(LocalMap map, LocalCoord position, byte amount)
    {
        if (!map.Contains(position))
        {
            return;
        }

        int index = map.GetIndex(position.X, position.Y);
        int combined = Intensity[index] + amount;
        Intensity[index] = (byte)Math.Min(255, combined);
    }

    public void Decay()
    {
        for (int i = 0; i < Intensity.Length; i++)
        {
            if (Intensity[i] > 0)
            {
                Intensity[i]--;
            }
        }
    }

    public byte GetIntensity(LocalMap map, LocalCoord position)
    {
        if (!map.Contains(position))
        {
            return 0;
        }

        return Intensity[map.GetIndex(position.X, position.Y)];
    }

    public byte GetMaxInRange(LocalMap map, LocalCoord origin, int chebyshevRange)
    {
        byte max = 0;
        for (int dy = -chebyshevRange; dy <= chebyshevRange; dy++)
        {
            for (int dx = -chebyshevRange; dx <= chebyshevRange; dx++)
            {
                var coord = new LocalCoord(origin.X + dx, origin.Y + dy);
                if (!map.Contains(coord))
                {
                    continue;
                }

                max = Math.Max(max, GetIntensity(map, coord));
            }
        }

        return max;
    }
}
