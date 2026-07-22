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
using NoMercy.Service.Hosting;
using Xunit;

namespace NoMercy.Tests.Service;

/// <summary>
/// The coordinator's token is the app-level graceful-shutdown signal every
/// BackgroundService's stoppingToken derives from. These lock the parts that do
/// not terminate the process: the initial state, that RequestShutdown cancels
/// the token, and that a repeat request is a safe no-op (the lock +
/// IsCancellationRequested guard). The Ctrl+C double-press path calls
/// Environment.Exit and is deliberately not exercised.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class ShutdownCoordinatorTests
{
    private static ShutdownCoordinator Build() => new(logger: NullLogger<ShutdownCoordinator>.Instance);

    [Fact]
    public void Token_BeforeShutdown_IsNotCancelled()
    {
        using ShutdownCoordinator sut = Build();

        Assert.False(condition: sut.Token.IsCancellationRequested);
    }

    [Fact]
    public void RequestShutdown_CancelsToken()
    {
        using ShutdownCoordinator sut = Build();

        sut.RequestShutdown();

        Assert.True(condition: sut.Token.IsCancellationRequested);
    }

    [Fact]
    public void RequestShutdown_CalledTwice_StaysCancelledAndDoesNotThrow()
    {
        using ShutdownCoordinator sut = Build();

        sut.RequestShutdown();
        Exception? second = Record.Exception(testCode: () => sut.RequestShutdown());

        Assert.Null(@object: second);
        Assert.True(condition: sut.Token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        ShutdownCoordinator sut = Build();

        Exception? disposed = Record.Exception(testCode: () => sut.Dispose());

        Assert.Null(@object: disposed);
    }
}
