namespace Game.Simulation.UI;

public sealed class MessageLog
{
    public void Restore(IEnumerable<string> messages)
    {
        _messages.Clear();
        foreach (string message in messages)
        {
            Add(message);
        }
    }

    public const int MaxPersistedMessages = 50;
    private readonly Queue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public void Add(string message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > MaxPersistedMessages)
        {
            _messages.Dequeue();
        }
    }

    public string[] Recent(int count)
    {
        return _messages.Reverse().Take(count).Reverse().ToArray();
    }
}
