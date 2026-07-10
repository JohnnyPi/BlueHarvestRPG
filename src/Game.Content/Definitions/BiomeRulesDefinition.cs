namespace Game.Content.Definitions;

public sealed class BiomeRulesDefinition
{
    public float OceanMaxElevation { get; set; } = 0.35f;
    public float BeachMaxElevation { get; set; } = 0.39f;
    public float MountainsMinElevation { get; set; } = 0.82f;
    public float SmallMountainMinElevation { get; set; } = 0.75f;
    public float HillsMinElevation { get; set; } = 0.68f;
    public float FoothillsMinElevation { get; set; } = 0.62f;
    public float SwampMinMoisture { get; set; } = 0.75f;
    public float ForestMinMoisture { get; set; } = 0.48f;
}
