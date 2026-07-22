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
[Trait(name: "Category", value: "Unit")]
public class StartupOptionsParsingTests
{
    [Theory]
    [InlineData(data: ["Debug", LogEventLevel.Debug])]
    [InlineData(data: ["debug", LogEventLevel.Debug])]
    [InlineData(data: ["VERBOSE", LogEventLevel.Verbose])]
    [InlineData(data: ["Warning", LogEventLevel.Warning])]
    [InlineData(data: ["error", LogEventLevel.Error])]
    [InlineData(data: ["Fatal", LogEventLevel.Fatal])]
    public void TryParseLogLevel_KnownLevel_ReturnsTrueAndValue(string raw, LogEventLevel expected)
    {
        bool ok = StartupOptions.TryParseLogLevel(raw: raw, level: out LogEventLevel level);

        Assert.True(condition: ok);
        Assert.Equal(expected: expected, actual: level);
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "   ")]
    [InlineData(data: "verbse")]
    [InlineData(data: "trace")]
    [InlineData(data: "loud")]
    [InlineData(data: "99")]
    public void TryParseLogLevel_UnknownOrEmpty_ReturnsFalseAndFallsBackToInformation(string? raw)
    {
        bool ok = StartupOptions.TryParseLogLevel(raw: raw, level: out LogEventLevel level);

        Assert.False(condition: ok);
        Assert.Equal(expected: LogEventLevel.Information, actual: level);
    }

    [Fact]
    public void ResolvePortFrom_CliPortSet_WinsOverDatabaseValue()
    {
        Assert.Equal(expected: 8000, actual: StartupOptions.ResolvePortFrom(cliPort: 8000, dbValue: "9000", fallback: 7626));
    }

    [Theory]
    [InlineData(data: ["9000", 9000])]
    [InlineData(data: ["7700", 7700])]
    public void ResolvePortFrom_NoCliPort_UsesValidDatabaseValue(string dbValue, int expected)
    {
        Assert.Equal(expected: expected, actual: StartupOptions.ResolvePortFrom(cliPort: 0, dbValue: dbValue, fallback: 7626));
    }

    [Theory]
    [InlineData(data: null)]
    [InlineData(data: "")]
    [InlineData(data: "not-a-number")]
    [InlineData(data: "70000000000")]
    public void ResolvePortFrom_NoCliPort_CorruptOrMissingDbValue_UsesFallback(string? dbValue)
    {
        Assert.Equal(expected: 7626, actual: StartupOptions.ResolvePortFrom(cliPort: 0, dbValue: dbValue, fallback: 7626));
    }
}
