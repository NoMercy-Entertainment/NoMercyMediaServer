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
        Assert.False(condition: options.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_ParsedFromArgs()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>(args:
        [
            "--service",
        ]);

        StartupOptions? parsed = null;
        result.WithParsed(action: o => parsed = o);

        Assert.NotNull(@object: parsed);
        Assert.True(condition: parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_FalseWhenNotProvided()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>(args: []);

        StartupOptions? parsed = null;
        result.WithParsed(action: o => parsed = o);

        Assert.NotNull(@object: parsed);
        Assert.False(condition: parsed.RunAsService);
    }

    [Fact]
    public void StartupOptions_RunAsService_CoexistsWithOtherFlags()
    {
        ParserResult<StartupOptions> result = Parser.Default.ParseArguments<StartupOptions>(args:
        [
            "--service",
            "--dev",
        ]);

        StartupOptions? parsed = null;
        result.WithParsed(action: o => parsed = o);

        Assert.NotNull(@object: parsed);
        Assert.True(condition: parsed.RunAsService);
        Assert.True(condition: parsed.Development);
    }

}
