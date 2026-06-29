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
using System.IO;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging.Rendering;

namespace NoMercy.NmSystem.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that renders every entry through
/// <see cref="ConsoleLineRenderer"/>. Colour is auto-detected (off when output is
/// redirected or NO_COLOR is set) unless forced via options. Writes are serialised
/// so interleaved lines from concurrent workers stay intact.
/// </summary>
public sealed class NoMercyLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly NoMercyLoggerOptions _options;
    private readonly TextWriter _output;
    private readonly object _gate = new();
    private readonly bool _color;
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
    }

    internal NoMercyLoggerOptions Options => _options;

    internal IExternalScopeProvider? ScopeProvider => _scopes;

    public ILogger CreateLogger(string categoryName) => new NoMercyLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose() { }

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

        lock (_gate)
        {
            _output.WriteLine(line);
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
}
