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

namespace NoMercy.Tests.NmSystem;

/// <summary>
/// Pins the NoMercy <see cref="ILogger"/>: source context maps to a category,
/// per-category levels gate output, scopes render as a dim suffix, and structured
/// message arguments are formatted.
/// </summary>
[Trait("Category", "Unit")]
public class NoMercyLoggerTests
{
    private static (ILogger Logger, StringWriter Sink, NoMercyLoggerProvider Provider) Build(
        string source,
        Action<NoMercyLoggerOptions>? configure = null
    )
    {
        NoMercyLoggerOptions options = new() { Color = false };
        configure?.Invoke(options);
        StringWriter sink = new();
        NoMercyLoggerProvider provider = new(options, sink);
        return (provider.CreateLogger(source), sink, provider);
    }

    [Fact]
    public void Log_RendersCategoryAndFormattedMessage()
    {
        (ILogger logger, StringWriter sink, _) = Build(
            "NoMercy.Providers.TMDB.Client.TmdbBaseClient"
        );

        logger.LogInformation("Fetching {Id}", 27205);

        sink.ToString().Should().Contain("TheMovieDB │ Fetching 27205");
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsSuppressed()
    {
        (ILogger logger, StringWriter sink, _) = Build(
            "NoMercy.Service.Whatever",
            o => o.MinimumLevel = LogLevel.Warning
        );

        logger.LogInformation("ignored");

        sink.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Log_CategoryOverride_EnablesBelowGlobalMinimum()
    {
        (ILogger logger, StringWriter sink, _) = Build(
            "NoMercy.Providers.TMDB.Client.TmdbBaseClient",
            o =>
            {
                o.MinimumLevel = LogLevel.Warning;
                o.CategoryLevels["moviedb"] = LogLevel.Debug;
            }
        );

        logger.LogDebug("debug detail");

        sink.ToString().Should().Contain("debug detail");
    }

    [Fact]
    public void Log_WithScope_AppendsDimSuffix()
    {
        NoMercyLoggerOptions options = new() { Color = false };
        StringWriter sink = new();
        NoMercyLoggerProvider provider = new(options, sink);
        provider.SetScopeProvider(new LoggerExternalScopeProvider());
        ILogger logger = provider.CreateLogger("NoMercy.Providers.TMDB.Client.TmdbBaseClient");

        using (logger.BeginScope("imp=7f3a"))
            logger.LogInformation("scoped");

        sink.ToString().TrimEnd().Should().EndWith("· imp=7f3a");
    }
}
