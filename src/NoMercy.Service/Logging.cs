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

using NoMercy.NmSystem.Logging;

namespace NoMercy.Service;

/// <summary>
/// The application's <see cref="ILogger{T}"/>. Forwards to the NoMercy console
/// logger (aligned, themed, structured) while filtering out noisy ASP.NET Core
/// framework messages. The category is derived from the source type so each
/// subsystem is coloured correctly.
/// </summary>
public class CustomLogger<T> : ILogger<T>
{
    private static readonly string[] FilteredPhrases =
    [
        "Middleware configuration started",
        "Wildcard detected",
        "Request matched endpoint",
        "The endpoint does not specify",
        "This request accepts compression",
        "Response compression",
        "The response will be compressed",
        "No response compression available",
        "All hosts are allowed",
        "Route pattern:",
        "Found protocol implementation",
        "Executing endpoint",
        "Executed endpoint",
        "Authorization was successful",
        "Authorization failed",
        "Request did not match any endpoints",
        "Microsoft",
    ];

    private readonly ILogger _inner;

    public CustomLogger(NoMercyLoggerProvider provider)
    {
        _inner = provider.CreateLogger(categoryName: typeof(T).FullName ?? typeof(T).Name);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => _inner.BeginScope(state: state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel: logLevel);

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

        // Errors bypass phrase filtering: "Microsoft" matches framework frames in
        // an exception's rendered stack, which was silently dropping 500 stacks.
        if (logLevel < LogLevel.Error)
        {
            string message = formatter(arg1: state, arg2: exception);
            if (FilteredPhrases.Any(predicate: message.Contains))
                return;
        }

        _inner.Log(logLevel: logLevel, eventId: eventId, state: state, exception: exception, formatter: formatter);
    }
}
