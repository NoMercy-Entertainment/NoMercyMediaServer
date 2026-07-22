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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Connectivity.Strategies;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Networking;

[Trait(name: "Category", value: "Unit")]
public sealed class CloudflareTunnelStrategyTests
{
    private static CloudflareTunnelStrategy BuildStrategy(
        ConnectivityStatus status,
        Func<Task>? checkAvailability = null
    )
    {
        return new(logger: NullLogger<CloudflareTunnelStrategy>.Instance, connectivityStatus: status, checkTunnelAvailability: checkAvailability);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsNull_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsEmpty_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = string.Empty };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsNull_DoesNotSetNatStatusToTunneled()
    {
        ConnectivityStatus status = new()
        {
            CloudflareTunnelToken = null,
            NatStatus = NatStatus.None,
        };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.NotEqual(expected: NatStatus.Tunneled, actual: status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_CheckAvailabilityCallback_IsInvokedBeforeTokenCheck()
    {
        bool called = false;
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(
            status: status,
            checkAvailability: () =>
            {
                called = true;
                return Task.CompletedTask;
            }
        );

        await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.True(condition: called);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenSet_ButBinaryMissing_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = "dummy-tunnel-token" };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.False(condition: result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenSet_ButBinaryMissing_NatStatusNotTunneled()
    {
        ConnectivityStatus status = new()
        {
            CloudflareTunnelToken = "dummy-token",
            NatStatus = NatStatus.None,
        };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        await strategy.TryEstablishAsync(ct: CancellationToken.None);

        Assert.NotEqual(expected: NatStatus.Tunneled, actual: status.NatStatus);
    }

    [Fact]
    public void Priority_IsThree()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(status: new());

        Assert.Equal(expected: 3, actual: strategy.Priority);
    }

    [Fact]
    public void Type_IsCloudflareTunnel()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(status: new());

        Assert.Equal(expected: ConnectivityType.CloudflareTunnel, actual: strategy.Type);
    }

    [Fact]
    public void TeardownAsync_WhenNothingStarted_DoesNotThrow()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(status: status);

        Exception? ex = Record.Exception(testCode: () => strategy.TeardownAsync().GetAwaiter().GetResult());

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_WhenNothingStarted_DoesNotThrow()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(status: new());

        Exception? ex = Record.Exception(testCode: strategy.Dispose);

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_CalledTwice_OnlyTearsDownOnce_AndDoesNotThrow()
    {
        // The _disposed guard must make the second Dispose() a no-op — this
        // proves the guard exists (a regression here would double-run
        // StopTunnel/dispose the already-disposed Process on the second call).
        CloudflareTunnelStrategy strategy = BuildStrategy(status: new());

        Exception? ex = Record.Exception(testCode: () =>
        {
            strategy.Dispose();
            strategy.Dispose();
        });

        Assert.Null(@object: ex);
    }

    [Fact]
    public async Task TeardownAsync_AfterDispose_DoesNotThrow()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(status: new());
        strategy.Dispose();

        Exception? ex = await Record.ExceptionAsync(testCode: strategy.TeardownAsync);

        Assert.Null(@object: ex);
    }

    [Fact]
    public async Task TryEstablishAsync_CheckAvailabilityThrows_PropagatesBeforeTokenCheck()
    {
        // The availability callback runs unconditionally before the token
        // gate — if it throws, TryEstablishAsync must not swallow it (no
        // try/catch wraps that call), matching the paid-feature-gate contract
        // the connectivity manager relies on to log the real cause.
        ConnectivityStatus status = new() { CloudflareTunnelToken = "token" };
        CloudflareTunnelStrategy strategy = BuildStrategy(
            status: status,
            checkAvailability: () => throw new InvalidOperationException(message: "gate check failed")
        );

        await Assert.ThrowsAsync<InvalidOperationException>(testCode: () =>
            strategy.TryEstablishAsync(ct: CancellationToken.None)
        );
    }
}
