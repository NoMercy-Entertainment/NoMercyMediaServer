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

using CommandLine;
using NoMercy.Service;

namespace NoMercy.Tests.Setup;

public class WindowsServiceHostTests
{
    [Fact]
    public void StartupOptions_RunAsService_DefaultsToFalse()
    {
        StartupOptions options = new();
        Assert.False(options.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_ParsedFromArgs()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>([
            "--service",
        ]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.True(parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_FalseWhenNotProvided()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>([]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.False(parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_CoexistsWithOtherFlags()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>([
            "--service",
            "--dev",
        ]);

        StartupOptions? parsed = null;
        result.WithParsed(o => parsed = o);

        Assert.NotNull(parsed);
        Assert.True(parsed.RunAsService);
        Assert.True(parsed.Development);
    }

}
