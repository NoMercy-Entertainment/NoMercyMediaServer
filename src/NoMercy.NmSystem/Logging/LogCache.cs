using NoMercy.NmSystem.Dto;

namespace NoMercy.NmSystem.Logging;

public static class LogCache
{
    private const int MaxEntries = 7;
    private static readonly Dictionary<string, List<LogEntry>?> Cache = new();

    public static bool TryGetCachedEntries(string filePath, out List<LogEntry>? cachedEntries)
    {
        return Cache.TryGetValue(filePath, out cachedEntries);
    }

    public static void AddToCache(string filePath, List<LogEntry>? entries)
    {
        if (!Cache.ContainsKey(filePath) && Cache.Count >= MaxEntries)
            Cache.Remove(Cache.Keys.First());

        Cache[filePath] = entries;
    }
}
