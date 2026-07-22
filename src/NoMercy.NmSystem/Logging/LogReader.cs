// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Text.Json;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using Serilog.Events;

namespace NoMercy.NmSystem.Logging;

public static class LogReader
{
    /// <summary>
    /// Reads every entry across both log formats the server writes: the legacy
    /// rolling <c>log*.txt</c> files (still the only sink for the static
    /// <see cref="Logger"/> API) and the current per-run <c>run-*.jsonl</c> files
    /// (the sink for every <c>ILogger&lt;T&gt;</c> call site, i.e. the majority of
    /// the codebase since the L1-L8 logging migration). Reading only <c>*.txt</c>
    /// made the dashboard's log search blind to almost everything actually logged
    /// today. Entries mirrored into a run file from the legacy bridge (same log
    /// call, two sinks) are deduplicated by (type, level, message, second).
    /// </summary>
    public static async Task<List<LogEntry>> GetLogsAsync(
        IStorage storage,
        string logDirectoryPath,
        Func<LogEntry, bool>? filter = null
    )
    {
        bool dirExists = await storage.ExistsAsync(path: logDirectoryPath, ct: CancellationToken.None);
        if (!dirExists)
            throw new DirectoryNotFoundException(message: $"Log directory not found: {logDirectoryPath}");

        IReadOnlyList<StorageEntry> textEntries = storage.List(path: logDirectoryPath, pattern: "*.txt", recursive: false);
        IReadOnlyList<StorageEntry> jsonlEntries = storage.List(
            path: logDirectoryPath,
            pattern: "run-*.jsonl",
            recursive: false
        );
        IOrderedEnumerable<StorageEntry> logFiles = textEntries
            .Concat(second: jsonlEntries)
            .OrderByDescending(keySelector: e => e.LastModified);

        List<LogEntry> logEntries = [];

        IEnumerable<Task<IEnumerable<LogEntry>>> tasks = logFiles.Select(selector: entry =>
            ProcessFileAsync(storage: storage, filePath: entry.Path, filter: filter)
        );
        IEnumerable<LogEntry>[] results = await Task.WhenAll(tasks: tasks);

        foreach (IEnumerable<LogEntry> chunk in results)
            logEntries.AddRange(collection: chunk);

        return logEntries
            .DistinctBy(keySelector: entry =>
                (
                    entry.Type,
                    entry.Level,
                    entry.Message,
                    Second: entry.Time.Ticks / TimeSpan.TicksPerSecond
                )
            )
            .ToList();
    }

    public static async Task<List<LogEntry>> GetLatestRunLogsAsync(
        IStorage storage,
        string logDirectoryPath,
        Func<LogEntry, bool>? filter = null
    )
    {
        bool dirExists = await storage.ExistsAsync(path: logDirectoryPath, ct: CancellationToken.None);
        if (!dirExists)
            return [];

        IReadOnlyList<StorageEntry> entries = storage.List(path: logDirectoryPath, pattern: "run-*.jsonl", recursive: false);
        StorageEntry? latest = entries
            .OrderByDescending(keySelector: e => e.LastModified)
            .ThenByDescending(keySelector: e => e.Path, comparer: StringComparer.Ordinal)
            .FirstOrDefault();
        if (latest is null)
            return [];

        IEnumerable<LogEntry> logs = await ProcessFileAsync(storage: storage, filePath: latest.Path, filter: filter);
        return logs.ToList();
    }

    private static async Task<IEnumerable<LogEntry>> ProcessFileAsync(
        IStorage storage,
        string filePath,
        Func<LogEntry, bool>? filter
    )
    {
        List<LogEntry> logEntries = new();

        if (!storage.Exists(path: filePath))
        {
            Logger.App(message: $"File not found: {filePath}", level: LogEventLevel.Warning);
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
                path: filePath,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.ReadWrite | FileShare.Delete
            );
            using StreamReader reader = new(stream: fileStream);

            while (await reader.ReadLineAsync() is { } line)
                try
                {
                    LogEntry? logEntry = JsonSerializer.Deserialize<LogEntry>(json: line);
                    if (logEntry != null && (filter == null || filter(arg: logEntry)))
                        logEntries.Add(item: logEntry);
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                }
        }
        catch (Exception ex)
        {
            Logger.App(message: $"Error processing file {filePath}: {ex.Message}", level: LogEventLevel.Error);
        }

        return logEntries;
    }
}
