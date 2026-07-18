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

using NoMercy.Service;
using Serilog.Events;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// A bad --loglevel / NOMERCY_LOG_LEVEL used to throw an uncaught ArgumentException
/// and crash startup, and a corrupt port row silently reverted to 7626. These pure
/// helpers keep both decisions total and never-throwing.
/// </summary>
[Trait("Category", "Unit")]
public class StartupOptionsParsingTests
{
    [Theory]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("VERBOSE", LogEventLevel.Verbose)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    public void TryParseLogLevel_KnownLevel_ReturnsTrueAndValue(string raw, LogEventLevel expected)
    {
        bool ok = StartupOptions.TryParseLogLevel(raw, out LogEventLevel level);

        Assert.True(ok);
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("verbse")]
    [InlineData("trace")]
    [InlineData("loud")]
    [InlineData("99")]
    public void TryParseLogLevel_UnknownOrEmpty_ReturnsFalseAndFallsBackToInformation(string? raw)
    {
        bool ok = StartupOptions.TryParseLogLevel(raw, out LogEventLevel level);

        Assert.False(ok);
        Assert.Equal(LogEventLevel.Information, level);
    }

    [Fact]
    public void ResolvePortFrom_CliPortSet_WinsOverDatabaseValue()
    {
        Assert.Equal(8000, StartupOptions.ResolvePortFrom(8000, "9000", 7626));
    }

    [Theory]
    [InlineData("9000", 9000)]
    [InlineData("7700", 7700)]
    public void ResolvePortFrom_NoCliPort_UsesValidDatabaseValue(string dbValue, int expected)
    {
        Assert.Equal(expected, StartupOptions.ResolvePortFrom(0, dbValue, 7626));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("70000000000")]
    public void ResolvePortFrom_NoCliPort_CorruptOrMissingDbValue_UsesFallback(string? dbValue)
    {
        Assert.Equal(7626, StartupOptions.ResolvePortFrom(0, dbValue, 7626));
    }
}
