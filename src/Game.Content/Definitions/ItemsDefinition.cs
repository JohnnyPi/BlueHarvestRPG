namespace Game.Content.Definitions;

public sealed class ItemsDefinition
{
    public List<ItemDefinition> Items { get; set; } = [];
}

public sealed class ItemDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
