using Game.Content.Definitions;

namespace Game.IslandPreview.Generation;

public sealed class PreviewGenerationRequest
{
    public required IslandDefinition Island { get; init; }
    public required BiomeRulesDefinition BiomeRules { get; init; }
    public required ulong Seed { get; init; }
}
