namespace Game.Content.Definitions;

public sealed class CharacterDefaultsDefinition
{
    public int StartingLevel { get; set; } = 1;
    public int StartingExperience { get; set; }
    public List<AttributeDefaultDefinition> Attributes { get; set; } = [];
}

public sealed class AttributeDefaultDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Default { get; set; }
}
