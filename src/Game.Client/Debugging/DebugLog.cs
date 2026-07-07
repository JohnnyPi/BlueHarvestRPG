using System.Diagnostics;

namespace Game.Client.Debugging;

public static class DebugLog
{
    private static readonly object Gate = new();
    private static string? _logPath;
    private static readonly HashSet<string> _recentIssues = new(StringComparer.Ordinal);

    public static void Initialize(string? saveDirectory = null)
    {
        string baseDir = saveDirectory is not null
            ? Path.GetDirectoryName(saveDirectory) ?? saveDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Game.Persistence.Saves.SaveManager.AppFolderName);

        Directory.CreateDirectory(baseDir);
        _logPath = Path.Combine(baseDir, "debug.log");
    }

    public static void Info(string message)
    {
        if (!DebugMode.IsEnabled)
        {
            return;
        }

        Write("INFO", message);
    }

    public static void Issue(string message)
    {
        if (!DebugMode.IsEnabled)
        {
            return;
        }

        lock (Gate)
        {
            if (!_recentIssues.Add(message))
            {
                return;
            }

            if (_recentIssues.Count > 256)
            {
                _recentIssues.Clear();
                _recentIssues.Add(message);
            }
        }

        Write("ISSUE", message);
    }

    public static void Exception(Exception exception, string context)
    {
        Write("ERROR", $"{context}: {exception}");
    }

    private static void Write(string level, string message)
    {
        string line = $"[{DateTime.UtcNow:O}] [{level}] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);

        if (_logPath is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never crash the game.
        }
    }
}
