using NoMercy.NmSystem.Information;

namespace NoMercy.Launcher.Services;

public static class LauncherLog
{
    private static readonly string LogPath = Path.Combine(AppFiles.AppPath, "launcher.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", message + (ex is not null ? $" | {ex}" : ""));

    private static void Write(string level, string message)
    {
        try
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // best effort
        }
    }
}
