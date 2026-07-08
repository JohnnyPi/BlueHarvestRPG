namespace Game.Generation.Noise;

public static class NoiseUtility
{
    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float Fbm(ulong seed, float x, float y, int octaves = 4, float persistence = 0.5f)
    {
        return ValueNoise.Sample(seed, x, y, octaves, persistence);
    }

    public static float RidgedNoise(ulong seed, float x, float y, int octaves = 4, float persistence = 0.5f)
    {
        float sum = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float weight = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float signal = 1f - MathF.Abs(ValueNoise.Sample(seed, x * frequency, y * frequency, octaves: 1) * 2f - 1f);
            signal *= signal;
            signal *= weight;
            weight = Math.Clamp(signal * 2f, 0f, 1f);
            sum += signal * amplitude;
            frequency *= 2f;
            amplitude *= persistence;
        }

        return sum;
    }

    public static (float X, float Y) LowFrequencyWarp(
        ulong seed,
        float x,
        float y,
        float frequency,
        float amplitude,
        int octaves)
    {
        float warpX = ValueNoise.Sample(seed, x * frequency, y * frequency, octaves) * amplitude;
        float warpY = ValueNoise.Sample(seed + 1, x * frequency + 17.3f, y * frequency + 9.1f, octaves) * amplitude;
        return (x + warpX, y + warpY);
    }

    public static (float X, float Y) DomainWarp(
        ulong seed,
        float x,
        float y,
        float largeStrength,
        float mediumStrength,
        float smallStrength)
    {
        float largeX = ValueNoise.Sample(seed, x * 1.5f, y * 1.5f, octaves: 3) * largeStrength;
        float largeY = ValueNoise.Sample(seed + 1, x * 1.5f, y * 1.5f, octaves: 3) * largeStrength;

        float medX = ValueNoise.Sample(seed + 2, x * 4f, y * 4f, octaves: 3) * mediumStrength;
        float medY = ValueNoise.Sample(seed + 3, x * 4f, y * 4f, octaves: 3) * mediumStrength;

        float fineX = ValueNoise.Sample(seed + 4, x * 12f, y * 12f, octaves: 2) * smallStrength;
        float fineY = ValueNoise.Sample(seed + 5, x * 12f, y * 12f, octaves: 2) * smallStrength;

        return (x + largeX + medX + fineX, y + largeY + medY + fineY);
    }
}
