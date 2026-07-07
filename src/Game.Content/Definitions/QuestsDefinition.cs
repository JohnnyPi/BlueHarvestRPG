namespace Game.Content.Definitions;

public sealed class QuestsDefinition
{
    public List<QuestDefinition> Quests { get; set; } = [];
}

public sealed class QuestDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public int Target { get; set; } = 1;
}
