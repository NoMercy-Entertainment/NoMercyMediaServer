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

using Microsoft.Extensions.Logging;

namespace NoMercy.NmSystem.Logging;

/// <summary>An <see cref="ILogger"/> backed by <see cref="NoMercyLoggerProvider"/>.</summary>
public sealed class NoMercyLogger : ILogger
{
    private readonly string _sourceContext;
    private readonly NoMercyLoggerProvider _provider;

    public NoMercyLogger(string sourceContext, NoMercyLoggerProvider provider)
    {
        _sourceContext = sourceContext;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => _provider.ScopeProvider?.Push(state: state) ?? NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None && logLevel >= EffectiveLevel();

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel: logLevel))
            return;

        string message = formatter(arg1: state, arg2: exception);
        if (string.IsNullOrEmpty(value: message) && exception is null)
            return;

        _provider.Write(timestamp: DateTime.Now, level: logLevel, sourceContext: _sourceContext, message: message, exception: exception);
    }

    private LogLevel EffectiveLevel()
    {
        LogCategory category = LogCategories.ResolveSource(sourceContext: _sourceContext);
        return _provider.Options.CategoryLevels.TryGetValue(key: category.Key, value: out LogLevel level)
            ? level
            : _provider.Options.MinimumLevel;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        private NullScope() { }

        public void Dispose() { }
    }
}
