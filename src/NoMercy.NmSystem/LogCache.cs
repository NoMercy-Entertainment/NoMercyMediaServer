using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem;

public static class LogCache
{
    private static readonly Dictionary<string, List<LogEntry>?> Cache = new();

    public static bool TryGetCachedEntries(string filePath, out List<LogEntry>? cachedEntries)
    {
        return Cache.TryGetValue(filePath, out cachedEntries);
    }

    public static void AddToCache(string filePath, List<LogEntry>? entries)
    {
        Cache[filePath] = entries;
    }
}