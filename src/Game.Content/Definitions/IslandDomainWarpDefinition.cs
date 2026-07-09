namespace Game.Content.Definitions;

public sealed class IslandDomainWarpDefinition
{
    /// <summary>Slow lobing warp frequency (normalized blob space).</summary>
    public float LobingFrequency { get; set; } = 0.8f;

    /// <summary>Slow lobing warp amplitude — primary meso-scale silhouette distortion.</summary>
    public float LobingAmplitude { get; set; } = 0.22f;

    /// <summary>Headland warp frequency applied after lobing.</summary>
    public float Frequency { get; set; } = 1.6f;

    /// <summary>Headland warp amplitude applied after lobing.</summary>
    public float Amplitude { get; set; } = 0.12f;

    public int Octaves { get; set; } = 3;

    /// <summary>Large-scale domain-warp strength (NoiseUtility.DomainWarp large octave).</summary>
    public float LargeStrength { get; set; } = 0.14f;

    /// <summary>Medium-scale domain-warp strength (headland detail).</summary>
    public float MediumStrength { get; set; } = 0.10f;

    /// <summary>Small-scale domain-warp strength (fine coastal wobble).</summary>
    public float SmallStrength { get; set; } = 0.04f;
}
