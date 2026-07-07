namespace Game.Content;

public static class ContentPaths
{
    public const string RootFolderName = "content";

    public static string ResolveRoot(string? overrideRoot = null)
    {
        if (!string.IsNullOrEmpty(overrideRoot))
        {
            return overrideRoot;
        }

        return Path.Combine(AppContext.BaseDirectory, RootFolderName);
    }
}
