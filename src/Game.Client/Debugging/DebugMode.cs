namespace Game.Client.Debugging;

public static class DebugMode
{
    public static bool IsEnabled { get; private set; }

    public static void EnableFromArgs(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--debug", StringComparison.OrdinalIgnoreCase)))
        {
            IsEnabled = true;
        }
    }

    public static void Toggle()
    {
        IsEnabled = !IsEnabled;
    }
}
