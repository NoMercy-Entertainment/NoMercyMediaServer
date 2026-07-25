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
using NoMercy.NmSystem.Logging;
using NoMercy.Service;

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
[Trait("Category", "Unit")]
public class CustomLoggerTests
{
    private static CustomLogger<CustomLoggerTests> BuildLogger(out StringWriter output)
    {
        output = new();
        NoMercyLoggerOptions options = new() { MinimumLevel = LogLevel.Trace };
        NoMercyLoggerProvider provider = new(options, output);
        return new(provider);
    }

    [Fact]
    public void BeginScope_DoesNotThrowAndReturnsADisposable()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(out _);

        IDisposable? scope = logger.BeginScope("correlation-id");

        scope.Should().NotBeNull();
        scope!.Dispose();
    }

    [Fact]
    public void Log_FilteredFrameworkPhrase_BelowError_IsSuppressed()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(out StringWriter output);

        logger.Log(
            LogLevel.Information,
            new EventId(0),
            "Middleware configuration started",
            null,
            (s, _) => s
        );

        output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Log_FilteredFrameworkPhrase_AtErrorLevel_StillLogs()
    {
        // Errors bypass phrase filtering — a "Microsoft" frame inside a real
        // exception's rendered stack must never be dropped.
        CustomLogger<CustomLoggerTests> logger = BuildLogger(out StringWriter output);

        logger.Log(
            LogLevel.Error,
            new EventId(0),
            "Unhandled exception in Microsoft.AspNetCore.Something",
            null,
            (s, _) => s
        );

        output.ToString().Should().NotBeEmpty();
    }

    [Fact]
    public void Log_NonFilteredMessage_IsLogged()
    {
        CustomLogger<CustomLoggerTests> logger = BuildLogger(out StringWriter output);

        logger.Log(LogLevel.Information, new EventId(0), "Server started", null, (s, _) => s);

        output.ToString().Should().Contain("Server started");
    }

    [Fact]
    public void Log_DisabledLevel_NeverInvokesFormatterOrWrites()
    {
        NoMercyLoggerOptions options = new() { MinimumLevel = LogLevel.Warning };
        StringWriter output = new();
        NoMercyLoggerProvider provider = new(options, output);
        CustomLogger<CustomLoggerTests> logger = new(provider);
        bool formatterCalled = false;

        logger.Log(
            LogLevel.Debug,
            new EventId(0),
            "state",
            null,
            (_, _) =>
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
            new NoMercyLoggerProvider(options, new StringWriter())
        );

        logger.IsEnabled(LogLevel.Information).Should().BeFalse();
        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
    }
}
