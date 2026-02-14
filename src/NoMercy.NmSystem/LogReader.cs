using System.Text.Json;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.NmSystem;

public static class LogReader
{
    public static async Task<List<LogEntry>> GetLogsAsync(
        string logDirectoryPath,
        int limit = 10,
        int page = 1,
        string? typeFilter = null,
        string? levelFilter = null,
        string? messageFilter = null)
    {
        if (!Directory.Exists(logDirectoryPath))
            throw new DirectoryNotFoundException($"Log directory not found: {logDirectoryPath}");

        IOrderedEnumerable<FileInfo> logFiles = GetLogFilesSortedByDate(logDirectoryPath);
        List<LogEntry> logEntries = [];

        IEnumerable<Task<IEnumerable<LogEntry>>> tasks = logFiles.Select(fileInfo =>
            ProcessFileAsync(fileInfo.FullName, typeFilter, levelFilter, messageFilter));
        IEnumerable<LogEntry>[] results = await Task.WhenAll(tasks);

        foreach (IEnumerable<LogEntry> entries in results) logEntries.AddRange(entries);

        logEntries = logEntries
            .OrderByDescending(entry => entry.Time)
            .Skip((page - 1) * limit)
            .Take(limit)
            .OrderBy(entry => entry.Time)
            .ToList();

        return logEntries;
    }

    private static IOrderedEnumerable<FileInfo> GetLogFilesSortedByDate(string logDirectoryPath)
    {
        return Directory.GetFiles(logDirectoryPath, "*.txt")
            .Select(file => new FileInfo(file))
            .OrderByDescending(f => f.LastWriteTime);
    }

    private static async Task<IEnumerable<LogEntry>> ProcessFileAsync(
        string filePath,
        string? typeFilter,
        string? levelFilter,
        string? messageFilter)
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
            await using FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(fileStream);

            while (await reader.ReadLineAsync() is { } line)
                try
                {
                    LogEntry? logEntry = JsonSerializer.Deserialize<LogEntry>(line);
                    if (logEntry != null &&
                        (typeFilter == null || logEntry.Type == typeFilter) &&
                        (levelFilter == null || logEntry.Level == levelFilter) &&
                        (messageFilter == null || logEntry.Message.Contains(messageFilter)))
                        logEntries.Add(logEntry);
                }
                catch (JsonException jsonEx)
                {
                    Logger.App($"Error deserializing line in file {filePath}: {jsonEx.Message}", LogEventLevel.Error);
                }
        }
        catch (Exception ex)
        {
            Logger.App($"Error processing file {filePath}: {ex.Message}", LogEventLevel.Error);
        }

        return logEntries;
    }
}