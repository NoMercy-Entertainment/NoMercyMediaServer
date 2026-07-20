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

using Moq;
using NoMercy.Cli;
using NoMercy.Cli.Commands;
using NoMercy.Tests.Cli.Support;
using Xunit;

namespace NoMercy.Tests.Cli.Commands;

/// <summary>
/// REQUIREMENT: <c>update</c> must only treat the server as "stopped" once
/// its management endpoint is PROVABLY unreachable — any successful response,
/// any ordinary error response, and a caller-driven cancellation must all be
/// treated as "still running" (or "cancel this operation", not "stopped").
/// Only a transport-level failure (connection refused, broken pipe) may flip
/// the file-swap over to "safe to apply". Getting this wrong risks the CLI
/// overwriting the running server's executable out from under it.
///
/// These invoke the private helpers directly via reflection with a mocked
/// <see cref="ICliClient"/> and short, explicit timeouts — the production
/// call site hardcodes a 30s timeout, which a mock-driven unit test must never
/// actually wait out.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UpdateCommandWaitForExitTests
{
    private static Task<bool> HasStoppedAsync(ICliClient client, CancellationToken ct) =>
        PrivateReflection.InvokeStaticAsync<bool>(
            typeof(UpdateCommand),
            "HasServerStoppedRespondingAsync",
            client,
            ct
        );

    private static Task<bool> WaitForExitAsync(
        ICliClient client,
        TimeSpan timeout,
        CancellationToken ct
    ) =>
        PrivateReflection.InvokeStaticAsync<bool>(
            typeof(UpdateCommand),
            "WaitForServerExitAsync",
            client,
            timeout,
            ct
        );

    [Fact]
    public async Task HasStoppedResponding_ClientAnswers_ReturnsFalse()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync("still alive");

        bool result = await HasStoppedAsync(client.Object, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasStoppedResponding_ClientThrowsGenericException_ReturnsTrue()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        bool result = await HasStoppedAsync(client.Object, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasStoppedResponding_CancelledWhileWaiting_ReturnsFalse_NotTrue()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        bool result = await HasStoppedAsync(client.Object, cts.Token);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitForExit_StopsRespondingImmediately_ReturnsTrue_WithoutWaitingFullTimeout()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        bool result = await WaitForExitAsync(
            client.Object,
            TimeSpan.FromSeconds(30),
            CancellationToken.None
        );

        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitForExit_NeverStopsResponding_TimesOut_ReturnsFalse()
    {
        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync("still alive");

        bool result = await WaitForExitAsync(
            client.Object,
            TimeSpan.FromMilliseconds(150),
            CancellationToken.None
        );

        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitForExit_CallerCancels_PropagatesCancellation_RatherThanReturningFalse()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Mock<ICliClient> client = new();
        client
            .Setup(c => c.GetRawAsync(ApiRoutes.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync("still alive");

        Func<Task> act = () => WaitForExitAsync(client.Object, TimeSpan.FromSeconds(30), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
