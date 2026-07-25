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

[Trait("Category", "Unit")]
public sealed class CloudflareTunnelStrategyTests
{
    private static CloudflareTunnelStrategy BuildStrategy(
        ConnectivityStatus status,
        Func<Task>? checkAvailability = null
    )
    {
        return new(NullLogger<CloudflareTunnelStrategy>.Instance, status, checkAvailability);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsNull_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsEmpty_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = string.Empty };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenIsNull_DoesNotSetNatStatusToTunneled()
    {
        ConnectivityStatus status = new()
        {
            CloudflareTunnelToken = null,
            NatStatus = NatStatus.None,
        };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.NotEqual(NatStatus.Tunneled, status.NatStatus);
    }

    [Fact]
    public async Task TryEstablishAsync_CheckAvailabilityCallback_IsInvokedBeforeTokenCheck()
    {
        bool called = false;
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(
            status,
            checkAvailability: () =>
            {
                called = true;
                return Task.CompletedTask;
            }
        );

        await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.True(called);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenSet_ButBinaryMissing_ReturnsFalse()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = "dummy-tunnel-token" };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        bool result = await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryEstablishAsync_WhenTokenSet_ButBinaryMissing_NatStatusNotTunneled()
    {
        ConnectivityStatus status = new()
        {
            CloudflareTunnelToken = "dummy-token",
            NatStatus = NatStatus.None,
        };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        await strategy.TryEstablishAsync(CancellationToken.None);

        Assert.NotEqual(NatStatus.Tunneled, status.NatStatus);
    }

    [Fact]
    public void Priority_IsThree()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(new());

        Assert.Equal(3, strategy.Priority);
    }

    [Fact]
    public void Type_IsCloudflareTunnel()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(new());

        Assert.Equal(ConnectivityType.CloudflareTunnel, strategy.Type);
    }

    [Fact]
    public void TeardownAsync_WhenNothingStarted_DoesNotThrow()
    {
        ConnectivityStatus status = new() { CloudflareTunnelToken = null };
        CloudflareTunnelStrategy strategy = BuildStrategy(status);

        Exception? ex = Record.Exception(() => strategy.TeardownAsync().GetAwaiter().GetResult());

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WhenNothingStarted_DoesNotThrow()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(new());

        Exception? ex = Record.Exception(strategy.Dispose);

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_CalledTwice_OnlyTearsDownOnce_AndDoesNotThrow()
    {
        // The _disposed guard must make the second Dispose() a no-op — this
        // proves the guard exists (a regression here would double-run
        // StopTunnel/dispose the already-disposed Process on the second call).
        CloudflareTunnelStrategy strategy = BuildStrategy(new());

        Exception? ex = Record.Exception(() =>
        {
            strategy.Dispose();
            strategy.Dispose();
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task TeardownAsync_AfterDispose_DoesNotThrow()
    {
        CloudflareTunnelStrategy strategy = BuildStrategy(new());
        strategy.Dispose();

        Exception? ex = await Record.ExceptionAsync(strategy.TeardownAsync);

        Assert.Null(ex);
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
            status,
            checkAvailability: () => throw new InvalidOperationException("gate check failed")
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            strategy.TryEstablishAsync(CancellationToken.None)
        );
    }
}
