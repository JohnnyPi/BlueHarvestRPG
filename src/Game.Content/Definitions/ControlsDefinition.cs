namespace Game.Content.Definitions;

public sealed class ActionBinding
{
    public List<string> Keyboard { get; set; } = [];
    public List<string> Mouse { get; set; } = [];
    public string? MouseWheel { get; set; }
}

public sealed class ControlsDefinition
{
    public Dictionary<string, ActionBinding> Actions { get; set; } = [];
}
