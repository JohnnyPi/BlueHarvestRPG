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
        if (_messages.Count == 0 || count <= 0)
        {
            return [];
        }

        int take = Math.Min(count, _messages.Count);
        var result = new string[take];
        int start = _messages.Count - take;
        int i = 0;
        foreach (string message in _messages)
        {
            if (start > 0)
            {
                start--;
                continue;
            }

            result[i++] = message;
            if (i >= take)
            {
                break;
            }
        }

        return result;
    }
}
