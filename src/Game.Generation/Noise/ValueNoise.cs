using Game.Simulation.Seeds;

namespace Game.Generation.Noise;

public static class ValueNoise
{
    public static float Sample(
        ulong seed,
        float x,
        float y,
        int octaves = 4,
        float persistence = 0.5f)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float total = 0f;
        float maxValue = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            float sample = SampleSingle(
                SeedUtility.Derive(seed, octave, 0, 0),
                x * frequency,
                y * frequency);

            total += sample * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }

        return total / maxValue;
    }

    private static float SampleSingle(ulong seed, float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        float tx = x - x0;
        float ty = y - y0;

        float v00 = HashToUnit(seed, x0, y0);
        float v10 = HashToUnit(seed, x1, y0);
        float v01 = HashToUnit(seed, x0, y1);
        float v11 = HashToUnit(seed, x1, y1);

        float ix0 = Lerp(v00, v10, SmoothStep(tx));
        float ix1 = Lerp(v01, v11, SmoothStep(tx));
        return Lerp(ix0, ix1, SmoothStep(ty));
    }

    private static float HashToUnit(ulong seed, int x, int y)
    {
        ulong hash = SeedUtility.Derive(seed, x, y, 0);
        return (hash >> 11) * (1.0f / (1UL << 53));
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
