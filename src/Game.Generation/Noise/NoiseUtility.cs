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
