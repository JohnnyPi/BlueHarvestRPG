namespace Game.Generation.Noise;

public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed == 0 ? 0xDEADBEEFCAFEBABEUL : seed;
    }

    public float NextFloat()
    {
        _state = Mix(_state);
        return (float)((_state >> 11) * (1.0 / (1UL << 53)));
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        int value = (int)(NextFloat() * maxExclusive);
        return value >= maxExclusive ? maxExclusive - 1 : value;
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 12;
            value *= 0x2545F4914F6CDD1DUL;
            value ^= value >> 32;
            value *= 0x5851F42D4C957F2DUL;
            value ^= value >> 29;
            return value;
        }
    }
}
