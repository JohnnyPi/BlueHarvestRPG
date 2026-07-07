namespace Game.Simulation.Character;

public sealed class CharacterProgress
{
    public int Level { get; set; } = 1;
    public int Experience { get; set; }
    public Dictionary<string, int> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static CharacterProgress CreateFromDefaults(
        int startingLevel,
        int startingExperience,
        IEnumerable<(string Id, int DefaultValue)> attributeDefaults)
    {
        var progress = new CharacterProgress
        {
            Level = startingLevel,
            Experience = startingExperience
        };

        foreach ((string id, int defaultValue) in attributeDefaults)
        {
            progress.Attributes[id] = defaultValue;
        }

        return progress;
    }

    public CharacterProgress Clone()
    {
        var copy = new CharacterProgress
        {
            Level = Level,
            Experience = Experience
        };

        foreach ((string key, int value) in Attributes)
        {
            copy.Attributes[key] = value;
        }

        return copy;
    }
}
