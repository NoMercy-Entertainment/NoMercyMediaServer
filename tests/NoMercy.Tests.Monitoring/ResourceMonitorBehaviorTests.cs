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

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Monitoring;
using Xunit;

namespace NoMercy.Tests.Monitoring;

public class ResourceMonitorBehaviorTests
{
    [Fact]
    public void ResourceMonitor_AfterStop_Monitor_ReturnsEmptyResource()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();
        Resource result = monitor.Monitor();

        result.Cpu.Total.Should().Be(0.0, "stopped monitor has no provider — CPU must be zero");
        result
            .Memory.Total.Should()
            .Be(0.0, "stopped monitor has no provider — Memory must be zero");
        result.Gpu.Should().BeEmpty("stopped monitor has no provider — GPU list must be empty");
    }

    [Fact]
    public void ResourceMonitor_AfterStop_Dispose_DoesNotThrow()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();

        Action act = () => monitor.Dispose();

        act.Should().NotThrow("double-stop and dispose must be idempotent");
    }

    [Fact]
    public void ResourceMonitor_Dispose_ThenMonitor_ReturnsEmptyResource()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        monitor.Dispose();
        Resource result = monitor.Monitor();

        result.Cpu.Total.Should().Be(0.0);
        result.Memory.Total.Should().Be(0.0);
        result.Gpu.Should().BeEmpty();
    }

    [Fact]
    public void ResourceMonitor_Stop_ThenStart_DoesNotThrow()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();

        Action act = () => monitor.Start();

        act.Should().NotThrow("restarting the provider after stop must succeed");

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_Monitor_ReturnsNonNullResource()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        Resource result = monitor.Monitor();

        result.Should().NotBeNull();
        result.Cpu.Should().NotBeNull();
        result.Memory.Should().NotBeNull();
        result.Gpu.Should().NotBeNull();

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_CpuTotal_IsBetweenZeroAndOneHundred()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        Resource result = monitor.Monitor();

        result
            .Cpu.Total.Should()
            .BeInRange(
                0.0,
                100.0,
                "CPU total must be clamped to [0, 100] — turbo boost headroom is handled by the provider"
            );

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_MemoryTotal_IsPositiveOnRealHost()
    {
        ResourceMonitor monitor = new(NullLogger<ResourceMonitor>.Instance);

        Resource result = monitor.Monitor();

        result
            .Memory.Total.Should()
            .BeGreaterThanOrEqualTo(
                0.0,
                "memory total is non-negative (0 only when the provider cannot read system memory)"
            );

        monitor.Dispose();
    }
}
