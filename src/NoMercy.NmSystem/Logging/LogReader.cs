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
        bool dirExists = await storage.ExistsAsync(logDirectoryPath, CancellationToken.None);
        if (!dirExists)
            throw new DirectoryNotFoundException($"Log directory not found: {logDirectoryPath}");

        IReadOnlyList<StorageEntry> textEntries = storage.List(logDirectoryPath, "*.txt", false);
        IReadOnlyList<StorageEntry> jsonlEntries = storage.List(
            logDirectoryPath,
            "run-*.jsonl",
            false
        );
        IOrderedEnumerable<StorageEntry> logFiles = textEntries
            .Concat(jsonlEntries)
            .OrderByDescending(e => e.LastModified);

        List<LogEntry> logEntries = [];

        IEnumerable<Task<IEnumerable<LogEntry>>> tasks = logFiles.Select(entry =>
            ProcessFileAsync(storage, entry.Path, filter)
        );
        IEnumerable<LogEntry>[] results = await Task.WhenAll(tasks);

        foreach (IEnumerable<LogEntry> chunk in results)
            logEntries.AddRange(chunk);

        return logEntries
            .DistinctBy(entry =>
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
        bool dirExists = await storage.ExistsAsync(logDirectoryPath, CancellationToken.None);
        if (!dirExists)
            return [];

        IReadOnlyList<StorageEntry> entries = storage.List(logDirectoryPath, "run-*.jsonl", false);
        StorageEntry? latest = entries
            .OrderByDescending(e => e.LastModified)
            .ThenByDescending(e => e.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (latest is null)
            return [];

        IEnumerable<LogEntry> logs = await ProcessFileAsync(storage, latest.Path, filter);
        return logs.ToList();
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
