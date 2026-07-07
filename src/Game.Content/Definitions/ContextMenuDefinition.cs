namespace Game.Content.Definitions;

public sealed class ContextMenuEntry
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
}

public sealed class ContextMenusDefinition
{
    public List<ContextMenuEntry> Overworld { get; set; } = [];
    public List<ContextMenuEntry> LocalMap { get; set; } = [];
}
