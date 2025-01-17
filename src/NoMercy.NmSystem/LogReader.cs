using Serilog.Events;

namespace NoMercy.NmSystem;

public static class LogReader
{
    public static async Task<List<LogEntry>> GetLastDailyLogsAsync(
        string logDirectoryPath,
        int limit = 10,
        Func<LogEntry, bool>? filter = null)
    {
        if (!Directory.Exists(logDirectoryPath))
            throw new DirectoryNotFoundException($"Log directory not found: {logDirectoryPath}");

        IOrderedEnumerable<FileInfo> logFiles = GetLogFilesSortedByDate(logDirectoryPath);
        List<LogEntry> logEntries = new();
        
        IEnumerable<Task<IEnumerable<LogEntry>>> tasks = logFiles.Select(fileInfo => ProcessFileAsync(fileInfo.FullName, limit, filter));
        IEnumerable<LogEntry>[] results = await Task.WhenAll(tasks);

        foreach (IEnumerable<LogEntry> entries in results)
        {
            logEntries.AddRange(entries);
            if (logEntries.Count >= limit) break;
        }

        return logEntries.Take(limit).ToList();
    }

    private static IOrderedEnumerable<FileInfo> GetLogFilesSortedByDate(string logDirectoryPath)
    {
        return Directory.GetFiles(logDirectoryPath, "*.txt")
            .Select(file => new FileInfo(file))
            .OrderByDescending(f => f.LastWriteTime);
    }

    private static async Task<IEnumerable<LogEntry>> ProcessFileAsync(
        string filePath,
        int limit,
        Func<LogEntry, bool>? filter)
    {
        List<LogEntry> logEntries = new();
        FileInfo fileInfo = new(filePath);

        if (!fileInfo.Exists)
        {
            Logger.App($"File not found: {filePath}", LogEventLevel.Warning);
            return logEntries;
        }

        try
        {
            if (LogCache.TryGetCachedEntries(filePath, out List<LogEntry>? cachedEntries) && 
                cachedEntries?.Count > 0 && 
                cachedEntries[0].Time >= fileInfo.LastWriteTime)
            {
                return cachedEntries.Where(entry => filter == null || filter(entry)).Take(limit);
            }

            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(fileStream);

            while (await reader.ReadLineAsync() is { } line && logEntries.Count < limit)
            {
                LogEntry? logEntry = line.FromJson<LogEntry>();
                if (logEntry != null && (filter == null || filter(logEntry)))
                {
                    logEntries.Add(logEntry);
                }
            }

            LogCache.AddToCache(filePath, logEntries);
        }
        catch (Exception ex)
        {
            Logger.App($"Error processing file {filePath}: {ex.Message}", LogEventLevel.Error);
        }

        return logEntries;
    }
}