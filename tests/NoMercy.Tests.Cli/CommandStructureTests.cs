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

using System.CommandLine;
using NoMercy.Cli;
using NoMercy.Cli.Commands;
using Xunit;

namespace NoMercy.Tests.Cli;

public class CommandStructureTests
{
    private readonly RootCommand _root;

    public CommandStructureTests()
    {
        _root = new(description: "NoMercy MediaServer CLI");

        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        _root.Options.Add(item: pipeOption);

        ICliClientFactory clientFactory = new CliClientFactory();

        _root.Subcommands.Add(item: StatusCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: LogsCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: StopCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: RestartCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: ConfigCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: PluginCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
        _root.Subcommands.Add(item: QueueCommand.Create(pipeOption: pipeOption, clientFactory: clientFactory));
    }

    [Fact]
    public void RootCommand_HasAllExpectedSubcommands()
    {
        List<string> names = _root.Subcommands.Select(selector: c => c.Name).ToList();

        Assert.Contains(expected: "status", collection: names);
        Assert.Contains(expected: "logs", collection: names);
        Assert.Contains(expected: "stop", collection: names);
        Assert.Contains(expected: "restart", collection: names);
        Assert.Contains(expected: "config", collection: names);
        Assert.Contains(expected: "plugin", collection: names);
        Assert.Contains(expected: "queue", collection: names);
        Assert.Equal(expected: 7, actual: names.Count);
    }

    [Fact]
    public void StatusCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "status");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesTailOption()
    {
        ParseResult result = _root.Parse(commandLine: "logs --tail 50");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesFollowOption()
    {
        ParseResult result = _root.Parse(commandLine: "logs --follow");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesShortAliases()
    {
        ParseResult result = _root.Parse(commandLine: "logs -n 20 -f");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesLevelFilter()
    {
        ParseResult result = _root.Parse(commandLine: "logs --level Error,Warning");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void LogsCommand_ParsesTypeFilter()
    {
        ParseResult result = _root.Parse(commandLine: "logs --type App");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void StopCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "stop");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void RestartCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "restart");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void ConfigGetCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "config get");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void ConfigSetCommand_ParsesKeyAndValue()
    {
        ParseResult result = _root.Parse(commandLine: "config set server_name MyServer");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void PluginListCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "plugin list");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void QueueStatusCommand_ParsesSuccessfully()
    {
        ParseResult result = _root.Parse(commandLine: "queue status");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void GlobalPipeOption_ParsesOnAnyCommand()
    {
        ParseResult result = _root.Parse(commandLine: "--pipe /tmp/test.sock status");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void GlobalPipeOption_ParsesShortAlias()
    {
        ParseResult result = _root.Parse(commandLine: "-p MyPipe status");
        Assert.Empty(collection: result.Errors);
    }

    [Fact]
    public void InvalidCommand_ProducesError()
    {
        ParseResult result = _root.Parse(commandLine: "nonexistent");
        Assert.NotEmpty(collection: result.Errors);
    }
}
