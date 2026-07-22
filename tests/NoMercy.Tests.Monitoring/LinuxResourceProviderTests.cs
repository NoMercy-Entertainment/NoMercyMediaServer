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

// LinuxResourceProvider carries [SupportedOSPlatform("linux")] as a documentation/
// analyzer hint for its production caller (ResourceMonitor, which only constructs
// it behind an OperatingSystem.IsLinux() gate). This test assembly deliberately
// calls it unconditionally, on whatever host runs the suite, BECAUSE every one of
// its OS-facing reads (/proc/stat, /proc/meminfo, /sys/class/drm) is already
// wrapped in its own try/catch that degrades to a zeroed/empty result — the
// contract under test is exactly that graceful degradation when the expected
// Linux surface is absent (e.g. a Windows dev box, or a Linux container without
// procfs/sysfs mounted). CA1416 is suppressed for that reason, not to paper over
// an unverified platform assumption.
#pragma warning disable CA1416

using System.Reflection;
using FluentAssertions;
using NoMercy.Monitoring;
using Xunit;

namespace NoMercy.Tests.Monitoring;

/// <summary>
/// Requirement: on any host where /proc and /sys are unavailable — every non-Linux
/// dev/CI box, and Linux containers without procfs/sysfs mounted — resource
/// collection must degrade to zeroed/empty values and must never throw. A crashing
/// resource monitor would take down the dashboard polling loop for an unrelated
/// reason (missing OS surface, not a real fault).
/// </summary>
public class LinuxResourceProviderTests
{
    private static LinuxResourceProvider CreateProvider() => new();

    [Fact]
    public void Constructor_OnHostWithoutProcfs_DoesNotThrow()
    {
        Action act = () => CreateProvider();

        act.Should().NotThrow(because: "the constructor's initial snapshot read must degrade, not crash");
    }

    [Fact]
    public void Collect_OnHostWithoutProcfs_CpuAndMemoryDegradeToZero()
    {
        LinuxResourceProvider provider = CreateProvider();

        Resource resource = provider.Collect();

        resource
            .Cpu.Total.Should()
            .Be(expected: 0.0, because: "/proc/stat is unavailable — CPU total must degrade to zero, not throw");
        resource.Cpu.Core.Should().BeEmpty(because: "no /proc/stat means no per-core samples can exist");
        resource
            .Memory.Total.Should()
            .Be(expected: 0.0, because: "/proc/meminfo is unavailable — memory must degrade to zero, not throw");
    }

    [Fact]
    public void Collect_TwiceInARow_DoesNotThrow_AndStaysZeroed()
    {
        // Exercises the "previous snapshot" delta path across two calls. Without
        // /proc/stat, ReadCpuSnapshots() returns an empty list on every call, so
        // _previousSnapshots never becomes non-empty and the delta branch is never
        // reached on this host — but the early-return path it takes instead must
        // remain stable across repeated calls (no accumulating state, no throw).
        LinuxResourceProvider provider = CreateProvider();

        provider.Collect();
        Resource second = provider.Collect();

        second.Cpu.Total.Should().Be(expected: 0.0);
    }

    [Fact]
    public void Collect_Gpu_IsWellFormedRegardlessOfHardwarePresence()
    {
        // GPU detection tries "nvidia-smi" via bare PATH lookup with no OS gate of
        // its own. On a host where nvidia-smi happens to be reachable (true here —
        // NVIDIA's Windows driver places nvidia-smi.exe in System32) this "Linux"
        // provider genuinely returns real GPU telemetry; on a host with neither an
        // NVIDIA driver nor AMD sysfs it returns none. Assert only the contract
        // that holds either way: well-formed entries, never a throw.
        LinuxResourceProvider provider = CreateProvider();

        Resource resource = provider.Collect();

        foreach (Gpu gpu in resource.Gpu)
        {
            gpu.Name.Should().NotBeNullOrWhiteSpace();
            gpu.Index.Should().BeGreaterThanOrEqualTo(expected: 0);
            gpu.Core.Should().BeGreaterThanOrEqualTo(expected: 0.0);
        }
    }

    // -----------------------------------------------------------------------
    // Pure private static helpers — reflected into directly (see
    // ReflectionHelpers) so their branch logic is demanded independently of any
    // live /proc/stat or /sys/class/drm content, which this test host does not
    // control.
    // -----------------------------------------------------------------------

    private static object CreateCpuSnapshot(
        string label,
        long user,
        long nice,
        long system,
        long idle,
        long ioWait,
        long irq,
        long softIrq,
        long steal
    ) =>
        ReflectionHelpers.CreateNested(
            outerType: typeof(LinuxResourceProvider),
            nestedTypeName: "CpuSnapshot", args: [label, user, nice, system, idle, ioWait, irq, softIrq, steal]
        );

    private static double CalculatePercent(object prev, object curr) =>
        (double)
            ReflectionHelpers.InvokeStatic(
                type: typeof(LinuxResourceProvider),
                methodName: "CalculatePercent", args: [prev, curr]
            )!;

    [Fact]
    public void CalculatePercent_ComputesBusyFractionOfDelta()
    {
        object prev = CreateCpuSnapshot(
            label: "cpu",
            user: 0,
            nice: 0,
            system: 0,
            idle: 1000,
            ioWait: 0,
            irq: 0,
            softIrq: 0,
            steal: 0
        );
        object curr = CreateCpuSnapshot(
            label: "cpu",
            user: 500,
            nice: 0,
            system: 0,
            idle: 1500,
            ioWait: 0,
            irq: 0,
            softIrq: 0,
            steal: 0
        );

        // prevTotal=1000 busy=0 ; currTotal=2000 busy=500 => delta 1000/500 = 50%
        double percent = CalculatePercent(prev: prev, curr: curr);

        percent.Should().Be(expected: 50.0);
    }

    [Fact]
    public void CalculatePercent_FullyIdleDelta_IsZero()
    {
        object prev = CreateCpuSnapshot(label: "cpu", user: 0, nice: 0, system: 0, idle: 1000, ioWait: 0, irq: 0, softIrq: 0, steal: 0);
        object curr = CreateCpuSnapshot(label: "cpu", user: 0, nice: 0, system: 0, idle: 2000, ioWait: 0, irq: 0, softIrq: 0, steal: 0);

        double percent = CalculatePercent(prev: prev, curr: curr);

        percent.Should().Be(expected: 0.0, because: "all of the delta went to Idle — busy delta is zero");
    }

    [Fact]
    public void CalculatePercent_WhenCounterDidNotAdvance_ReturnsZero_NotNegativeOrNaN()
    {
        // A counter reset (reboot, wraparound) can make curr.Total <= prev.Total.
        // The guard must return 0, never divide by a non-positive delta.
        object prev = CreateCpuSnapshot(label: "cpu", user: 100, nice: 0, system: 0, idle: 900, ioWait: 0, irq: 0, softIrq: 0, steal: 0);
        object curr = CreateCpuSnapshot(label: "cpu", user: 100, nice: 0, system: 0, idle: 900, ioWait: 0, irq: 0, softIrq: 0, steal: 0);

        double percent = CalculatePercent(prev: prev, curr: curr);

        percent.Should().Be(expected: 0.0);
    }

    [Fact]
    public void CalculatePercent_FullyBusyDelta_IsOneHundred()
    {
        object prev = CreateCpuSnapshot(label: "cpu", user: 0, nice: 0, system: 0, idle: 1000, ioWait: 0, irq: 0, softIrq: 0, steal: 0);
        object curr = CreateCpuSnapshot(label: "cpu", user: 1000, nice: 0, system: 0, idle: 1000, ioWait: 0, irq: 0, softIrq: 0, steal: 0);

        double percent = CalculatePercent(prev: prev, curr: curr);

        percent.Should().Be(expected: 100.0);
    }

    [Theory]
    [InlineData(data: [new[] { "cpu", "10", "20", "30" }, 1, 10L])]
    [InlineData(data: [new[] { "cpu", "10", "20", "30" }, 3, 30L])]
    [InlineData(data: [new[] { "cpu", "10", "20", "30" }, 9, 0L])] // out of range
    [InlineData(data: [new[] { "cpu", "not-a-number" }, 1, 0L])] // unparsable
    public void ParseLong_HandlesBoundsAndMalformedInput(string[] parts, int index, long expected)
    {
        long result = (long)
            ReflectionHelpers.InvokeStatic(
                type: typeof(LinuxResourceProvider),
                methodName: "ParseLong", args: [parts, index]
            )!;

        result.Should().Be(expected: expected);
    }

    [Theory]
    [InlineData(data: ["55.55", 55.6])]
    [InlineData(data: ["0", 0.0])]
    [InlineData(data: ["100", 100.0])]
    [InlineData(data: ["not-a-number", 0.0])]
    [InlineData(data: ["", 0.0])]
    public void ParseDouble_RoundsToOneDecimal_OrZeroOnFailure(string input, double expected)
    {
        double result = (double)
            ReflectionHelpers.InvokeStatic(type: typeof(LinuxResourceProvider), methodName: "ParseDouble", args: input)!;

        result.Should().Be(expected: expected);
    }

    [Fact]
    public void ReadAmdGpuName_WhenProductNameFileExists_ReturnsItsContent()
    {
        string cardPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-amd-card-{Guid.NewGuid():N}");
        string devicePath = Path.Combine(path1: cardPath, path2: "device");
        Directory.CreateDirectory(path: devicePath);
        File.WriteAllText(path: Path.Combine(path1: devicePath, path2: "product_name"), contents: "Radeon RX 6800\n");

        try
        {
            string name = (string)
                ReflectionHelpers.InvokeStatic(
                    type: typeof(LinuxResourceProvider),
                    methodName: "ReadAmdGpuName", args: [cardPath, 7]
                )!;

            name.Should().Be(expected: "Radeon RX 6800");
        }
        finally
        {
            Directory.Delete(path: cardPath, recursive: true);
        }
    }

    [Fact]
    public void ReadAmdGpuName_WhenProductNameFileMissing_FallsBackToGpuIndex()
    {
        string cardPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-amd-card-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: cardPath);

        try
        {
            string name = (string)
                ReflectionHelpers.InvokeStatic(
                    type: typeof(LinuxResourceProvider),
                    methodName: "ReadAmdGpuName", args: [cardPath, 3]
                )!;

            name.Should().Be(expected: "GPU 3");
        }
        finally
        {
            Directory.Delete(path: cardPath, recursive: true);
        }
    }

    [Fact]
    public void ReadAmdGpuName_WhenProductNameFileIsBlank_FallsBackToGpuIndex()
    {
        string cardPath = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-amd-card-{Guid.NewGuid():N}");
        string devicePath = Path.Combine(path1: cardPath, path2: "device");
        Directory.CreateDirectory(path: devicePath);
        File.WriteAllText(path: Path.Combine(path1: devicePath, path2: "product_name"), contents: "   \n");

        try
        {
            string name = (string)
                ReflectionHelpers.InvokeStatic(
                    type: typeof(LinuxResourceProvider),
                    methodName: "ReadAmdGpuName", args: [cardPath, 9]
                )!;

            name.Should()
                .Be(expected: "GPU 9", because: "a blank product_name file must fall back, not surface whitespace");
        }
        finally
        {
            Directory.Delete(path: cardPath, recursive: true);
        }
    }

    [Fact]
    public void ReadCpuSnapshots_OnHostWithoutProcStat_ReturnsEmptyList()
    {
        object result = ReflectionHelpers.InvokeStatic(
            type: typeof(LinuxResourceProvider),
            methodName: "ReadCpuSnapshots"
        )!;

        ((System.Collections.ICollection)result).Count.Should().Be(expected: 0);
    }

    [Fact]
    public void TryCollectAmdGpu_WithoutSysfs_LeavesGpuDictionaryEmpty()
    {
        Resource resource = new()
        {
            Cpu = new() { Core = [] },
            Memory = new(),
        };

        ReflectionHelpers.InvokeStatic(type: typeof(LinuxResourceProvider), methodName: "TryCollectAmdGpu", args: resource);

        resource
            .Gpu.Should()
            .BeEmpty(because: "/sys/class/drm does not exist off Linux — no AMD GPU can surface");
    }

    [Fact]
    public void TryCollectNvidiaGpu_DoesNotThrow_RegardlessOfDriverPresence()
    {
        Resource resource = new()
        {
            Cpu = new() { Core = [] },
            Memory = new(),
        };

        Action act = () =>
            ReflectionHelpers.InvokeStatic(
                type: typeof(LinuxResourceProvider),
                methodName: "TryCollectNvidiaGpu",
                args: resource
            );

        act.Should()
            .NotThrow(
                because: "nvidia-smi may or may not be reachable on the host — either way this must not throw"
            );
    }
}
