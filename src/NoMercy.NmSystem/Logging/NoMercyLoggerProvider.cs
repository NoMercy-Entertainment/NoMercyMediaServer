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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging.Rendering;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that renders every entry through
/// <see cref="ConsoleLineRenderer"/>, appends a rich JSON line to a per-run file
/// (one file per process start, oldest pruned), and optionally hands a
/// <see cref="NoMercyLogRecord"/> to a callback (dashboard / event bus). Colour
/// auto-detects (off when redirected or NO_COLOR is set). Writes are serialised.
/// </summary>
public sealed class NoMercyLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly NoMercyLoggerOptions _options;
    private readonly TextWriter _output;
    private readonly object _gate = new();
    private readonly bool _color;
    private readonly StreamWriter? _file;
    private IExternalScopeProvider? _scopes;

    public NoMercyLoggerProvider(NoMercyLoggerOptions options, TextWriter? output = null)
    {
        _options = options;
        _output = output ?? System.Console.Out;
        _color =
            options.Color
            ?? (
                !System.Console.IsOutputRedirected
                && Environment.GetEnvironmentVariable("NO_COLOR") is null
            );

        if (!string.IsNullOrEmpty(options.LogDirectory))
        {
            Directory.CreateDirectory(options.LogDirectory);
            string runId =
                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-"
                + Environment.ProcessId;
            string runFile = Path.Combine(options.LogDirectory, $"run-{runId}.jsonl");
            _file = new StreamWriter(runFile, append: true) { AutoFlush = true };
            PruneRuns(options.LogDirectory, options.MaxRunFiles);
        }
    }

    internal NoMercyLoggerOptions Options => _options;

    internal IExternalScopeProvider? ScopeProvider => _scopes;

    public ILogger CreateLogger(string categoryName) => new NoMercyLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
        lock (_gate)
        {
            _file?.Dispose();
        }
    }

    internal void Write(
        DateTime timestamp,
        LogLevel level,
        string sourceContext,
        string message,
        Exception? exception
    )
    {
        LogCategory category = LogCategories.ResolveSource(sourceContext);
        string? scope = CollectScope();
        string line = ConsoleLineRenderer.Render(
            timestamp,
            level,
            category,
            message,
            scope,
            exception,
            _options.Theme,
            _color,
            _options.WidthProvider()
        );

        LogFileLine fileLine = new()
        {
            Timestamp = timestamp.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            Type = category.Key,
            Category = category.DisplayName,
            Group = category.Group,
            Level = ToSerilogLevelName(level),
            LevelValue = (int)level,
            Color = category.DarkHex,
            Message = message,
            Scope = scope,
            Source = sourceContext,
            ThreadId = Environment.CurrentManagedThreadId,
            Exception = exception?.ToString(),
        };

        NoMercyLogRecord record = new(
            timestamp,
            level,
            category.Key,
            category.DisplayName,
            message,
            scope,
            exception?.ToString()
        );

        lock (_gate)
        {
            _output.WriteLine(line);
            _file?.WriteLine(JsonSerializer.Serialize(fileLine, JsonOptions));
        }

        if (_options.OnRecord is not null)
        {
            try
            {
                _options.OnRecord(record);
            }
            catch
            {
                // A failing log consumer must never break the logging path.
            }
        }
    }

    private string? CollectScope()
    {
        if (_scopes is null)
            return null;

        List<string> parts = new();
        _scopes.ForEachScope(
            static (scope, state) =>
            {
                string text = scope?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                    state.Add(text);
            },
            parts
        );

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string ToSerilogLevelName(LogLevel level) =>
        level switch
        {
            LogLevel.Trace => "Verbose",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Critical => "Fatal",
            _ => "Information",
        };

    private static void PruneRuns(string directory, int keep)
    {
        if (keep <= 0)
            return;

        try
        {
            List<string> files = Directory
                .GetFiles(directory, "run-*.jsonl")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .ToList();

            foreach (string old in files.Skip(keep))
            {
                try
                {
                    File.Delete(old);
                }
                catch
                {
                    // Another process may hold an older run file; skip it.
                }
            }
        }
        catch
        {
            // Pruning is best-effort; never fail startup over it.
        }
    }
}
