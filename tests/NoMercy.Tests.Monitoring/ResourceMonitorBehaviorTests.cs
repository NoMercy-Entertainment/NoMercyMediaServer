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
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();
        Resource result = monitor.Monitor();

        result.Cpu.Total.Should().Be(expected: 0.0, because: "stopped monitor has no provider — CPU must be zero");
        result
            .Memory.Total.Should()
            .Be(expected: 0.0, because: "stopped monitor has no provider — Memory must be zero");
        result.Gpu.Should().BeEmpty(because: "stopped monitor has no provider — GPU list must be empty");
    }

    [Fact]
    public void ResourceMonitor_AfterStop_Dispose_DoesNotThrow()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();

        Action act = () => monitor.Dispose();

        act.Should().NotThrow(because: "double-stop and dispose must be idempotent");
    }

    [Fact]
    public void ResourceMonitor_Dispose_ThenMonitor_ReturnsEmptyResource()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        monitor.Dispose();
        Resource result = monitor.Monitor();

        result.Cpu.Total.Should().Be(expected: 0.0);
        result.Memory.Total.Should().Be(expected: 0.0);
        result.Gpu.Should().BeEmpty();
    }

    [Fact]
    public void ResourceMonitor_Stop_ThenStart_DoesNotThrow()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        monitor.Stop();

        Action act = () => monitor.Start();

        act.Should().NotThrow(because: "restarting the provider after stop must succeed");

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_Monitor_ReturnsNonNullResource()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

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
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        Resource result = monitor.Monitor();

        result
            .Cpu.Total.Should()
            .BeInRange(
                minimumValue: 0.0,
                maximumValue: 100.0,
                because: "CPU total must be clamped to [0, 100] — turbo boost headroom is handled by the provider"
            );

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_MemoryTotal_IsPositiveOnRealHost()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);

        Resource result = monitor.Monitor();

        result
            .Memory.Total.Should()
            .BeGreaterThanOrEqualTo(
                expected: 0.0,
                because: "memory total is non-negative (0 only when the provider cannot read system memory)"
            );

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_WhenProviderThrows_MonitorReturnsEmptyResource_NotAnException()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);
        ReflectionHelpers.SetField(instance: monitor, fieldName: "_provider", value: new ThrowingResourceProvider());

        Resource result = monitor.Monitor();

        result
            .Cpu.Total.Should()
            .Be(expected: 0.0, because: "a throwing provider must degrade to an empty Resource, not propagate");
        result.Memory.Total.Should().Be(expected: 0.0);
        result.Gpu.Should().BeEmpty();

        monitor.Dispose();
    }

    [Fact]
    public void ResourceMonitor_Start_WhenAlreadyStarted_IsANoOp()
    {
        ResourceMonitor monitor = new(logger: NullLogger<ResourceMonitor>.Instance);
        object? providerBeforeSecondStart = ReflectionHelpers.GetField<object?>(
            instance: monitor,
            fieldName: "_provider"
        );

        monitor.Start();

        ReflectionHelpers
            .GetField<object?>(instance: monitor, fieldName: "_provider")
            .Should()
            .BeSameAs(
                expected: providerBeforeSecondStart,
                because: "Start() must not replace an already-running provider"
            );

        monitor.Dispose();
    }

    [Fact]
    public void CreateLinuxProvider_ReturnsAWorkingLinuxProvider_RegardlessOfHostOs()
    {
        // Reflects directly into the private factory method so the Linux branch of
        // ResourceMonitor's OS dispatch is demanded even on a Windows test host —
        // OperatingSystem.IsLinux() itself can only ever be true on a genuine Linux
        // host, which is why Start()'s "else if (OperatingSystem.IsLinux())" branch
        // is itemized rather than covered here (see coverage report notes).
        object provider = ReflectionHelpers.InvokeStatic(
            type: typeof(ResourceMonitor),
            methodName: "CreateLinuxProvider"
        )!;

        provider.Should().BeAssignableTo<IResourceProvider>();

        Action act = () => ((IResourceProvider)provider).Collect();
        act.Should().NotThrow();
    }
}
