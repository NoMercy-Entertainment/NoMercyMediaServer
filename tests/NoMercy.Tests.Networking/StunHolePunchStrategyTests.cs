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

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Connectivity;
using NoMercy.Networking.Connectivity.Strategies;
using NoMercy.NmSystem.Configuration;
using NoMercy.NmSystem.Status;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: StunHolePunchStrategy must never throw out of
/// TryEstablishAsync — a local UDP bind failure (port already in use) has to
/// be caught and reported as "did not establish" (false), same as Dispose/
/// TeardownAsync being safe to call on a strategy that never got as far as
/// opening a socket. The live STUN round trip against real public servers
/// (NAT-type classification: full-cone vs restricted vs symmetric) requires
/// actual internet access to stun.l.google.com / stun.cloudflare.com and is
/// itemized as not unit-testable — see the coverage report.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class StunHolePunchStrategyTests
{
    [Fact]
    public void Name_IsStunHolePunch()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

        Assert.Equal(expected: "StunHolePunch", actual: strategy.Name);
    }

    [Fact]
    public void Priority_IsTwo()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

        Assert.Equal(expected: 2, actual: strategy.Priority);
    }

    [Fact]
    public void Type_IsStunHolePunch()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

        Assert.Equal(expected: ConnectivityType.StunHolePunch, actual: strategy.Type);
    }

    [Fact]
    public async Task TryEstablishAsync_LocalPortAlreadyBound_ReturnsFalse_WithoutThrowing()
    {
        // Bind the exact local port the strategy will try to use first, so
        // its own `new UdpClient(localPort)` hits a real SocketException
        // (address already in use) — no mock, a genuine bind conflict.
        int originalPort = RuntimeServerSettings.Current.InternalServerPort;
        UdpClient blocker = new(port: 0);
        int freePort = ((IPEndPoint)blocker.Client.LocalEndPoint!).Port;

        try
        {
            RuntimeServerSettings.Current.InternalServerPort = freePort - 1; // StunPort = InternalServerPort + 1
            StunHolePunchStrategy strategy = new(
                logger: NullLogger<StunHolePunchStrategy>.Instance,
                connectivityStatus: new ConnectivityStatus()
            );

            bool result = await strategy.TryEstablishAsync(ct: CancellationToken.None);

            Assert.False(condition: result);
        }
        finally
        {
            blocker.Dispose();
            RuntimeServerSettings.Current.InternalServerPort = originalPort;
        }
    }

    [Fact]
    public async Task TeardownAsync_WhenNothingStarted_DoesNotThrow()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

        Exception? ex = await Record.ExceptionAsync(testCode: strategy.TeardownAsync);

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_WhenNothingStarted_DoesNotThrow()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

        Exception? ex = Record.Exception(testCode: strategy.Dispose);

        Assert.Null(@object: ex);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );

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
        StunHolePunchStrategy strategy = new(
            logger: NullLogger<StunHolePunchStrategy>.Instance,
            connectivityStatus: new ConnectivityStatus()
        );
        strategy.Dispose();

        Exception? ex = await Record.ExceptionAsync(testCode: strategy.TeardownAsync);

        Assert.Null(@object: ex);
    }
}
