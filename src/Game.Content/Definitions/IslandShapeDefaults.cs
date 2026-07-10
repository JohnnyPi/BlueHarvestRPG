namespace Game.Content.Definitions;

public static class IslandShapeDefaults
{
    public static IslandShapeDefinition CreateNublar() => new()
    {
        UnionSmoothness = 0.18f,
        SubtractSmoothness = 0.12f,
        LandThreshold = 0.02f,
        AdditiveBlobs =
        [
            new IslandBlobDefinition
            {
                Name = "north_mass",
                Center = [-0.12f, 0.30f],
                Radius = [0.78f, 0.55f],
                RotationDegrees = -8f,
                Strength = 1.0f,
                Smoothness = 0.20f
            },
            new IslandBlobDefinition
            {
                Name = "east_lobe",
                Center = [0.32f, 0.07f],
                Radius = [0.45f, 0.42f],
                RotationDegrees = 12f,
                Strength = 0.85f,
                Smoothness = 0.20f
            },
            new IslandBlobDefinition
            {
                Name = "south_taper",
                Center = [-0.08f, -0.38f],
                Radius = [0.32f, 0.62f],
                RotationDegrees = -5f,
                Strength = 0.75f,
                Smoothness = 0.18f
            }
        ],
        SubtractiveBays =
        [
            new IslandBlobDefinition
            {
                Name = "southeast_bay",
                Center = [0.68f, -0.10f],
                Radius = [0.34f, 0.24f],
                RotationDegrees = 0f,
                Strength = 0.75f,
                Smoothness = 0.12f
            },
            new IslandBlobDefinition
            {
                Name = "northeast_lagoon",
                Center = [0.52f, 0.50f],
                Radius = [0.28f, 0.17f],
                RotationDegrees = 0f,
                Strength = 0.55f,
                Smoothness = 0.10f
            },
            new IslandBlobDefinition
            {
                Name = "west_cove",
                Center = [-0.64f, 0.02f],
                Radius = [0.18f, 0.25f],
                RotationDegrees = 0f,
                Strength = 0.35f,
                Smoothness = 0.10f
            }
        ],
        DomainWarp = new IslandDomainWarpDefinition
        {
            LobingFrequency = 0.8f,
            LobingAmplitude = 0.22f,
            Frequency = 1.6f,
            Amplitude = 0.12f,
            Octaves = 3,
            LargeStrength = 0.14f,
            MediumStrength = 0.10f,
            SmallStrength = 0.04f
        },
        CoastlineDetail = new IslandCoastlineDetailDefinition
        {
            Frequency = 5f,
            Amplitude = 0.025f,
            PreserveLargeBays = true,
            CellularAutomataIterations = 1,
            ProceduralInletCount = 0,
            PreferRiverMouthInlets = true
        }
    };

    public static IReadOnlyList<IslandRidgeDefinition> CreateNublarRidges() =>
    [
        new IslandRidgeDefinition
        {
            Name = "main_ridge",
            Points =
            [
                [-0.25f, 0.42f],
                [0.00f, 0.27f],
                [0.25f, 0.07f],
                [0.45f, -0.13f]
            ],
            Strength = 0.28f,
            Width = 0.14f
        },
        new IslandRidgeDefinition
        {
            Name = "east_ridge",
            Points =
            [
                [0.15f, 0.22f],
                [0.32f, 0.17f],
                [0.48f, 0.14f]
            ],
            Strength = 0.18f,
            Width = 0.10f
        },
        new IslandRidgeDefinition
        {
            Name = "south_ridge",
            Points =
            [
                [0.05f, 0.17f],
                [0.00f, -0.08f],
                [-0.02f, -0.33f]
            ],
            Strength = 0.16f,
            Width = 0.10f
        }
    ];
}
