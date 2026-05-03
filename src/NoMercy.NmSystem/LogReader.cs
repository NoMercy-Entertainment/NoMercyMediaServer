using System.Text.Json;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem;

public static class LogReader
{
    public static async Task<List<LogEntry>> GetLogsAsync(
        IStorage storage,
        string logDirectoryPath,
        Func<LogEntry, bool>? filter = null
    )
    {
        bool dirExists = await storage.ExistsAsync(logDirectoryPath, CancellationToken.None);
        if (!dirExists)
            throw new DirectoryNotFoundException($"Log directory not found: {logDirectoryPath}");

        IReadOnlyList<StorageEntry> entries = storage.List(logDirectoryPath, "*.txt", false);
        IOrderedEnumerable<StorageEntry> logFiles = entries.OrderByDescending(e => e.LastModified);

        List<LogEntry> logEntries = [];

        IEnumerable<Task<IEnumerable<LogEntry>>> tasks = logFiles.Select(entry =>
            ProcessFileAsync(storage, entry.Path, filter)
        );
        IEnumerable<LogEntry>[] results = await Task.WhenAll(tasks);

        foreach (IEnumerable<LogEntry> chunk in results)
            logEntries.AddRange(chunk);

        return logEntries;
    }

    private static async Task<IEnumerable<LogEntry>> ProcessFileAsync(
        IStorage storage,
        string filePath,
        Func<LogEntry, bool>? filter
    )
    {
        List<LogEntry> logEntries = new();

        if (!storage.Exists(filePath))
        {
            Logger.App($"File not found: {filePath}", LogEventLevel.Warning);
            return logEntries;
        }

        try
        {
            // Serilog holds log.txt open with FileShare.ReadWrite|Delete (we
            // pass shared:true to the file sink). storage.OpenRead defaults
            // to FileShare.Read, which the OS rejects against Serilog's live
            // writer — every reload of the dashboard's log view hit
            // 'The process cannot access the file ... because it is being
            // used by another process'. Open the log file directly with
            // ReadWrite|Delete so co-existing readers/writers don't collide.
            await using FileStream fileStream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using StreamReader reader = new(fileStream);

            while (await reader.ReadLineAsync() is { } line)
                try
                {
                    LogEntry? logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (logEntry != null && (filter == null || filter(logEntry)))
                        logEntries.Add(logEntry);
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                }
        }
        catch (Exception ex)
        {
            Logger.App($"Error processing file {filePath}: {ex.Message}", LogEventLevel.Error);
        }

        return logEntries;
    }
}
