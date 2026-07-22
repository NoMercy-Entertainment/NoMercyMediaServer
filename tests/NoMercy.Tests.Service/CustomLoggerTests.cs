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

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NoMercy.NmSystem.Logging;
using NoMercy.Service;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// <see cref="CustomLogger{T}"/> replaces the framework's <see cref="ILogger{T}"/>
/// so every subsystem's log lines reach the NoMercy console/file sink even
/// though <c>WebHostFactory</c> calls <c>builder.Logging.ClearProviders()</c>.
/// It must filter noisy ASP.NET Core framework INFO/DEBUG chatter while never
/// filtering an actual error (a filtered 500 stack trace is a silent outage),
/// and <see cref="ILogger.BeginScope{TState}"/> must forward to the real
/// provider rather than being a no-op stub — job base classes rely on scopes
/// for correlated log lines.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class CustomLoggerTests
{
    private static CustomLogger<CustomLoggerTests> BuildLogger(out StringWriter output)
    {
        output = new();
        NoMercyLoggerOptions options = new() { MinimumLevel = LogLevel.Trace };
        NoMercyLoggerProvider provider = new(options: options, output: output);
        return new(provider: provider);
    }

    [Fact]
    public void BeginScope_DoesNotThrowAndReturnsADisposable()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(output: out _);

        IDisposable? scope = logger.BeginScope(state: "correlation-id");

        scope.Should().NotBeNull();
        scope!.Dispose();
    }

    [Fact]
    public void Log_FilteredFrameworkPhrase_BelowError_IsSuppressed()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(output: out StringWriter output);

        logger.Log(
            logLevel: LogLevel.Information,
            eventId: new EventId(id: 0),
            state: "Middleware configuration started",
            exception: null,
            formatter: (s, _) => s
        );

        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Log_FilteredFrameworkPhrase_AtErrorLevel_StillLogs()
    {
        // Errors bypass phrase filtering — a "Microsoft" frame inside a real
        // exception's rendered stack must never be dropped.
        CustomLogger<CustomLoggerTests> logger = BuildLogger(output: out StringWriter output);

        logger.Log(
            logLevel: LogLevel.Error,
            eventId: new EventId(id: 0),
            state: "Unhandled exception in Microsoft.AspNetCore.Something",
            exception: null,
            formatter: (s, _) => s
        );

        output.ToString().Should().NotBeEmpty();
    }

    [Fact]
    public void Log_NonFilteredMessage_IsLogged()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(output: out StringWriter output);

        logger.Log(logLevel: LogLevel.Information, eventId: new EventId(id: 0), state: "Server started", exception: null, formatter: (s, _) => s);

        output.ToString().Should().Contain(expected: "Server started");
    }

    [Fact]
    public void Log_DisabledLevel_NeverInvokesFormatterOrWrites()
    {
        NoMercyLoggerOptions options = new() { MinimumLevel = LogLevel.Warning };
        StringWriter output = new();
        NoMercyLoggerProvider provider = new(options: options, output: output);
        CustomLogger<CustomLoggerTests> logger = new(provider: provider);
        bool formatterCalled = false;

        logger.Log(
            logLevel: LogLevel.Debug,
            eventId: new EventId(id: 0),
            state: "state",
            exception: null,
            formatter: (_, _) =>
            {
                formatterCalled = true;
                return "irrelevant";
            }
        );

        formatterCalled.Should().BeFalse();
        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void IsEnabled_ReflectsProviderMinimumLevel()
    {
        NoMercyLoggerOptions options = new() { MinimumLevel = LogLevel.Warning };
        CustomLogger<CustomLoggerTests> logger = new(
            provider: new NoMercyLoggerProvider(options: options, output: new StringWriter())
        );

        logger.IsEnabled(logLevel: LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(logLevel: LogLevel.Warning).Should().BeTrue();
    }
}
