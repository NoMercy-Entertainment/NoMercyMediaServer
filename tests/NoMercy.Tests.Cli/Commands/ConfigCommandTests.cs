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
using Moq;
using NoMercy.Cli;
using NoMercy.Cli.Commands;
using NoMercy.Cli.Models;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>config get</c> must fail distinctly when unreachable and
/// otherwise print every field the server reports. <c>config set &lt;key&gt;
/// &lt;value&gt;</c> must translate the key to snake_case and the value to its
/// narrowest JSON type (int, then bool, else string) before sending it —
/// getting either translation wrong silently corrupts the value the server
/// receives.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class ConfigCommandTests
{
    private static async Task<int> RunGetAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: ConfigCommand.Create(pipeOption: pipeOption, clientFactory: factory));
        return await root.Parse(args: ["config", "get"]).InvokeAsync();
    }

    private static async Task<int> RunSetAsync(ICliClientFactory factory, string key, string value)
    {
        Option<string?> pipeOption = new(name: "--pipe", aliases: "-p");
        RootCommand root = new(description: "test");
        root.Options.Add(item: pipeOption);
        root.Subcommands.Add(item: ConfigCommand.Create(pipeOption: pipeOption, clientFactory: factory));
        return await root.Parse(args: ["config", "set", key, value]).InvokeAsync();
    }

    [Fact]
    public async Task Get_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<ConfigResponse>(ApiRoutes.Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (ConfigResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunGetAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Error.Should().Contain(expected: "Could not connect to server.");
    }

    [Fact]
    public async Task Get_ServerReachable_PrintsEveryField()
    {
        ConfigResponse config = new()
        {
            ServerName = "nomercy-test",
            InternalPort = 7626,
            ExternalPort = 7627,
            QueueWorkers = 2,
            EncoderWorkers = 1,
            CronWorkers = 1,
            DataWorkers = 3,
            ImageWorkers = 10,
            FileWorkers = 4,
            RequestWorkers = 5,
            Swagger = true,
        };

        Mock<ICliClient> client = new();
        client
            .Setup(expression: c => c.GetAsync<ConfigResponse>(ApiRoutes.Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: config);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunGetAsync(factory: factory.Object);

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        console.Out.Should().Contain(expected: "Server Name:      nomercy-test");
        console.Out.Should().Contain(expected: "Internal Port:    7626");
        console.Out.Should().Contain(expected: "External Port:    7627");
        console.Out.Should().Contain(expected: "Queue Workers:    2");
        console.Out.Should().Contain(expected: "Encoder Workers:  1");
        console.Out.Should().Contain(expected: "Cron Workers:     1");
        console.Out.Should().Contain(expected: "Data Workers:     3");
        console.Out.Should().Contain(expected: "Image Workers:    10");
        console.Out.Should().Contain(expected: "File Workers:     4");
        console.Out.Should().Contain(expected: "Request Workers:  5");
        console.Out.Should().Contain(expected: "Swagger:          True");
    }

    [Theory]
    [InlineData(data: ["serverName", "server_name"])]
    [InlineData(data: ["queueWorkers", "queue_workers"])]
    public async Task Set_TranslatesKeyToSnakeCase_InPayload(string key, string expectedKey)
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                action: (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(value: true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunSetAsync(factory: factory.Object, key: key, value: "value");

        exitCode.Should().Be(expected: (int)ExitCode.Success);
        capturedJson.Should().Contain(expected: $"\"{expectedKey}\"");
        console.Out.Should().Contain(expected: $"Configuration updated: {key} = value");
    }

    [Theory]
    [InlineData(data: ["42", "42"])]
    [InlineData(data: ["-7", "-7"])]
    public async Task Set_IntegerValue_SerializesAsJsonNumber(
        string value,
        string expectedJsonValue
    )
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                action: (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(value: true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory: factory.Object, key: "internal_port", value: value);

        capturedJson.Should().Be(expected: $"{{\"internal_port\":{expectedJsonValue}}}");
    }

    [Theory]
    [InlineData(data: "true")]
    [InlineData(data: "false")]
    public async Task Set_BooleanValue_SerializesAsJsonBoolean(string value)
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                action: (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(value: true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory: factory.Object, key: "swagger", value: value);

        capturedJson.Should().Be(expected: $"{{\"swagger\":{value}}}");
    }

    [Fact]
    public async Task Set_NonNumericNonBooleanValue_SerializesAsJsonString()
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                action: (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(value: true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory: factory.Object, key: "server_name", value: "MyServer");

        capturedJson.Should().Be(expected: """{"server_name":"MyServer"}""");
    }

    [Fact]
    public async Task Set_NotAcknowledged_ReturnsServerError_WithoutSuccessMessage()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(expression: c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(value: false);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(expression: f => f.Create(It.IsAny<string?>())).Returns(value: client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunSetAsync(factory: factory.Object, key: "server_name", value: "MyServer");

        exitCode.Should().Be(expected: (int)ExitCode.ServerError);
        console.Out.Should().NotContain(unexpected: "updated");
    }
}
