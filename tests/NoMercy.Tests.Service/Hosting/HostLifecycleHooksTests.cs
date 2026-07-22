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

using System.Diagnostics;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NoMercy.NmSystem.Status;
using NoMercy.Service.Hosting;
using Xunit;

namespace NoMercy.Tests.Service.Hosting;

/// <summary>
/// <see cref="HostLifecycleHooks.Register"/> wires the two boot-time signals every
/// other subsystem depends on: <see cref="IBootStatus.MarkStarted"/> (the health
/// endpoint's readiness flag) must fire exactly when the host actually finishes
/// starting, and the boot stopwatch must stop at that same moment — not before,
/// not never. A missed <c>MarkStarted</c> call would leave Docker's HEALTHCHECK
/// failing forever even though the server is genuinely up.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class HostLifecycleHooksTests
{
    private static WebApplication BuildApp(IBootStatus bootStatus)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(implementationInstance: bootStatus);
        return builder.Build();
    }

    [Fact]
    public async Task Register_ApplicationStarted_MarksBootStatusStarted()
    {
        Mock<IBootStatus> bootStatus = new();
        WebApplication app = BuildApp(bootStatus: bootStatus.Object);
        Stopwatch stopwatch = new();
        stopwatch.Start();

        HostLifecycleHooks.Register(app: app, stopWatch: stopwatch);

        await app.StartAsync();
        try
        {
            bootStatus.Verify(expression: b => b.MarkStarted(), times: Times.Once);
            stopwatch.IsRunning.Should().BeFalse();
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Register_BeforeApplicationStarted_NeverCallsMarkStarted()
    {
        Mock<IBootStatus> bootStatus = new();
        WebApplication app = BuildApp(bootStatus: bootStatus.Object);
        Stopwatch stopwatch = new();
        stopwatch.Start();

        HostLifecycleHooks.Register(app: app, stopWatch: stopwatch);

        bootStatus.Verify(expression: b => b.MarkStarted(), times: Times.Never);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Register_ApplicationStopping_DoesNotThrow()
    {
        Mock<IBootStatus> bootStatus = new();
        WebApplication app = BuildApp(bootStatus: bootStatus.Object);
        Stopwatch stopwatch = new();

        HostLifecycleHooks.Register(app: app, stopWatch: stopwatch);
        await app.StartAsync();

        Func<Task> stopping = async () => await app.StopAsync();

        await stopping.Should().NotThrowAsync();
        await app.DisposeAsync();
    }
}
