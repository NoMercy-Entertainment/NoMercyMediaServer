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

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Logging.Rendering;
using Serilog.Events;
using LegacyLogger = NoMercy.NmSystem.SystemCalls.Logger;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that renders every entry through
/// <see cref="ConsoleLineRenderer"/>, appends a rich JSON line to a per-run file
/// (one file per process start, oldest pruned), and optionally hands a
/// <see cref="NoMercyLogRecord"/> to a callback (dashboard / event bus). When
/// <see cref="NoMercyLoggerOptions.BridgeLegacyLogger"/> is set it also captures
/// entries from the legacy static logger into the same per-run file (transitional).
/// Colour auto-detects (off when redirected or NO_COLOR is set). Writes are serialised.
/// </summary>
public sealed class NoMercyLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly NoMercyLoggerOptions _options;
    private readonly TextWriter _output;
    private readonly object _gate = new();
    private readonly bool _color;
    private readonly StreamWriter? _file;
    private readonly bool _bridged;
    private bool _disposed;
    private IExternalScopeProvider? _scopes;

    public NoMercyLoggerProvider(NoMercyLoggerOptions options, TextWriter? output = null)
    {
        _options = options;
        _output = output ?? Console.Out;
        _color =
            options.Color
            ?? (
                !Console.IsOutputRedirected
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
            _file = new(runFile, append: true) { AutoFlush = true };
            PruneRuns(options.LogDirectory, options.MaxRunFiles);
        }

        if (options.BridgeLegacyLogger)
        {
            LegacyLogger.ConsoleSink = RenderLegacyConsole;
            if (_file is not null)
                LegacyLogger.LogEmitted += WriteEntry;
            _bridged = true;
        }
    }

    internal NoMercyLoggerOptions Options => _options;

    internal IExternalScopeProvider? ScopeProvider => _scopes;

    public ILogger CreateLogger(string categoryName) => new NoMercyLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
        if (_bridged)
        {
            LegacyLogger.ConsoleSink = null;
            LegacyLogger.LogEmitted -= WriteEntry;
        }

        lock (_gate)
        {
            _disposed = true;
            _file?.Dispose();
        }
    }

    /// <summary>
    /// Writes a legacy <see cref="LogEntry"/> (from the static logger) to the per-run file only.
    /// Console output for legacy entries is still produced by the legacy logger itself.
    /// </summary>
    public void WriteEntry(LogEntry entry)
    {
        if (_file is null)
            return;

        LogCategory category = LogCategories.Resolve(entry.Type);
        LogFileLine fileLine = new()
        {
            Timestamp = entry.Time.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            Type = category.Key,
            Category = category.DisplayName,
            Group = category.Group,
            Level = entry.Level,
            LevelValue = (int)entry.LogLevel,
            Color = string.IsNullOrEmpty(entry.Color) ? category.DarkHex : entry.Color,
            Message = entry.Message,
            Scope = null,
            Source = entry.Type,
            ThreadId = entry.ThreadId,
            Exception = null,
        };

        lock (_gate)
        {
            if (!_disposed)
                _file.WriteLine(JsonSerializer.Serialize(fileLine, JsonOptions));
        }
    }

    /// <summary>Renders a legacy <see cref="LogEntry"/> to the console through the
    /// unified <see cref="ConsoleLineRenderer"/> so legacy and ILogger output share one format.</summary>
    private void RenderLegacyConsole(LogEntry entry)
    {
        LogCategory category = LogCategories.Resolve(entry.Type);
        string line = ConsoleLineRenderer.Render(
            entry.Time.ToLocalTime(),
            ToMelLevel(entry.LogLevel),
            category,
            entry.Message,
            null,
            _options.Theme,
            _color,
            _options.WidthProvider()
        );

        lock (_gate)
        {
            _output.WriteLine(line);
        }
    }

    private static LogLevel ToMelLevel(LogEventLevel level) =>
        level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            LogEventLevel.Fatal => LogLevel.Critical,
            _ => LogLevel.Information,
        };

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

        // A logger resolved from an already-disposed DI container (e.g. the HTTP
        // host's ServerRunner logging after the HTTPS restart swap) must degrade to
        // console-only, never throw — an ObjectDisposedException here took down the
        // whole process at the last step of first-boot setup.
        lock (_gate)
        {
            _output.WriteLine(line);
            if (!_disposed)
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
