namespace Game.Simulation.Seeds;

public static class SeedUtility
{
    public static ulong DeriveStage(ulong worldSeed, uint stageId)
    {
        unchecked
        {
            ulong hash = worldSeed ^ stageId * 0x165667B19E3779F9UL;
            return Mix(hash);
        }
    }

    public static ulong Derive(
        ulong worldSeed,
        int x,
        int y,
        uint generatorVersion)
    {
        unchecked
        {
            ulong hash = worldSeed;

            hash ^= (ulong)x * 0x9E3779B185EBCA87UL;
            hash = RotateLeft(hash, 27);

            hash ^= (ulong)y * 0xC2B2AE3D27D4EB4FUL;
            hash = RotateLeft(hash, 31);

            hash ^= generatorVersion * 0x165667B19E3779F9UL;

            return Mix(hash);
        }
    }

    /// <summary>
    /// Stable FNV-1a string hash. string.GetHashCode is randomized per process, so it must
    /// never feed seeds; this keeps generation reproducible across runs and saves.
    /// </summary>
    public static ulong HashString(string value)
    {
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            return Mix(hash);
        }
    }

    private static ulong RotateLeft(ulong value, int amount)
    {
        return (value << amount) | (value >> (64 - amount));
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return value;
    }
}
