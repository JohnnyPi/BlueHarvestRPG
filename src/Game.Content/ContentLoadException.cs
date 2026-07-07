namespace Game.Content;

public sealed class ContentLoadException : Exception
{
    public ContentLoadException(string message)
        : base(message)
    {
    }

    public ContentLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
