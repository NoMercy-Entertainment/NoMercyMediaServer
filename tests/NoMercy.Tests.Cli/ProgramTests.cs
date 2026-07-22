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

using NoMercy.Cli;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli;

/// <summary>
/// REQUIREMENT: this drives the REAL entry point (<c>Program.Main</c>), not a
/// re-implementation of its command wiring — a subcommand silently dropped
/// from <c>Program.cs</c> (it happened once already: the existing
/// <c>CommandStructureTests</c> fixture rebuilds the tree by hand and is
/// missing <c>resources</c>/<c>autostart</c>/<c>update</c>) must fail here.
/// Every scenario is chosen so the real parser rejects the input BEFORE any
/// command action runs — malformed input must never reach the network layer,
/// so these can assert a non-zero exit code without a live management server.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class ProgramTests
{
    [Fact]
    public async Task Main_Help_ExitsZero_AndListsEveryRegisteredSubcommand()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["--help"]);

        exitCode.Should().Be(expected: 0);
        console.Out.Should().Contain(expected: "start");
        console.Out.Should().Contain(expected: "status");
        console.Out.Should().Contain(expected: "logs");
        console.Out.Should().Contain(expected: "stop");
        console.Out.Should().Contain(expected: "restart");
        console.Out.Should().Contain(expected: "config");
        console.Out.Should().Contain(expected: "plugin");
        console.Out.Should().Contain(expected: "queue");
        console.Out.Should().Contain(expected: "resources");
        console.Out.Should().Contain(expected: "autostart");
        console.Out.Should().Contain(expected: "update");
    }

    [Fact]
    public async Task Main_GlobalPipeOption_ComposesWithHelp_WithoutError()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["--pipe", "custom-pipe", "--help"]);

        exitCode.Should().Be(expected: 0);
    }

    [Fact]
    public async Task Main_UnknownTopLevelCommand_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["frobnicate"]);

        exitCode.Should().NotBe(unexpected: 0);
        console.Error.ToString().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Main_UnknownGlobalFlag_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["--this-flag-does-not-exist"]);

        exitCode.Should().NotBe(unexpected: 0);
    }

    [Fact]
    public async Task Main_ConfigSetMissingKeyAndValue_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["config", "set"]);

        exitCode.Should().NotBe(unexpected: 0);
    }

    [Fact]
    public async Task Main_LogsTailGivenNonNumericValue_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["logs", "--tail", "not-a-number"]);

        exitCode.Should().NotBe(unexpected: 0);
    }

    [Fact]
    public async Task Main_LogsTailGivenOutOfRangeValue_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["logs", "--tail", "99999999999999999999999"]);

        exitCode.Should().NotBe(unexpected: 0);
    }

    [Fact]
    public async Task Main_PipeOptionMissingValue_ReturnsNonZeroExitCode()
    {
        using ConsoleCapture console = new();
        int exitCode = await Program.Main(args: ["--pipe"]);

        exitCode.Should().NotBe(unexpected: 0);
    }

    [Fact]
    public async Task Main_NoArguments_DoesNotThrow()
    {
        using ConsoleCapture console = new();
        Exception? ex = await Record.ExceptionAsync(testCode: async () => await Program.Main(args: []));

        ex.Should().BeNull();
    }
}
