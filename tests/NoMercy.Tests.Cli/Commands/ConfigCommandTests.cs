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
[Trait("Category", "Unit")]
public sealed class ConfigCommandTests
{
    private static async Task<int> RunGetAsync(ICliClientFactory factory)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(ConfigCommand.Create(pipeOption, factory));
        return await root.Parse(["config", "get"]).InvokeAsync();
    }

    private static async Task<int> RunSetAsync(ICliClientFactory factory, string key, string value)
    {
        Option<string?> pipeOption = new("--pipe", "-p");
        RootCommand root = new("test");
        root.Options.Add(pipeOption);
        root.Subcommands.Add(ConfigCommand.Create(pipeOption, factory));
        return await root.Parse(["config", "set", key, value]).InvokeAsync();
    }

    [Fact]
    public async Task Get_ServerUnreachable_PrintsError_AndReturnsServerError()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetAsync<ConfigResponse>(ApiRoutes.Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigResponse?)null);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunGetAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Error.Should().Contain("Could not connect to server.");
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
            .Setup(c => c.GetAsync<ConfigResponse>(ApiRoutes.Config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunGetAsync(factory.Object);

        exitCode.Should().Be((int)ExitCode.Success);
        console.Out.Should().Contain("Server Name:      nomercy-test");
        console.Out.Should().Contain("Internal Port:    7626");
        console.Out.Should().Contain("External Port:    7627");
        console.Out.Should().Contain("Queue Workers:    2");
        console.Out.Should().Contain("Encoder Workers:  1");
        console.Out.Should().Contain("Cron Workers:     1");
        console.Out.Should().Contain("Data Workers:     3");
        console.Out.Should().Contain("Image Workers:    10");
        console.Out.Should().Contain("File Workers:     4");
        console.Out.Should().Contain("Request Workers:  5");
        console.Out.Should().Contain("Swagger:          True");
    }

    [Theory]
    [InlineData("serverName", "server_name")]
    [InlineData("queueWorkers", "queue_workers")]
    public async Task Set_TranslatesKeyToSnakeCase_InPayload(string key, string expectedKey)
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunSetAsync(factory.Object, key, "value");

        exitCode.Should().Be((int)ExitCode.Success);
        capturedJson.Should().Contain($"\"{expectedKey}\"");
        console.Out.Should().Contain($"Configuration updated: {key} = value");
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("-7", "-7")]
    public async Task Set_IntegerValue_SerializesAsJsonNumber(
        string value,
        string expectedJsonValue
    )
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory.Object, "internal_port", value);

        capturedJson.Should().Be($"{{\"internal_port\":{expectedJsonValue}}}");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task Set_BooleanValue_SerializesAsJsonBoolean(string value)
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory.Object, "swagger", value);

        capturedJson.Should().Be($"{{\"swagger\":{value}}}");
    }

    [Fact]
    public async Task Set_NonNumericNonBooleanValue_SerializesAsJsonString()
    {
        string? capturedJson = null;
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .Callback<string, HttpContent?, CancellationToken>(
                (_, content, _) => capturedJson = content!.ReadAsStringAsync().Result
            )
            .ReturnsAsync(true);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture _ = new();
        await RunSetAsync(factory.Object, "server_name", "MyServer");

        capturedJson.Should().Be("""{"server_name":"MyServer"}""");
    }

    [Fact]
    public async Task Set_NotAcknowledged_ReturnsServerError_WithoutSuccessMessage()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c =>
                c.PutAsync(ApiRoutes.Config, It.IsAny<HttpContent>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false);

        Mock<ICliClientFactory> factory = new();
        factory.Setup(f => f.Create(It.IsAny<string?>())).Returns(client.Object);

        using ConsoleCapture console = new();
        int exitCode = await RunSetAsync(factory.Object, "server_name", "MyServer");

        exitCode.Should().Be((int)ExitCode.ServerError);
        console.Out.Should().NotContain("updated");
    }
}
