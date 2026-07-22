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

using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

/// <summary>
/// Branch-coverage gaps for <see cref="ProcessResourceMonitor"/>:
///
/// • SampleGpu returns an empty list (no vendor sampler available by default).
/// • SampleGpu logs the "unsupported" warning EXACTLY ONCE — the
///   _gpuWarningLogged flag prevents log spam on repeated calls.
/// • NullResourceMonitor.SampleGpu also returns empty (parity check).
/// • Concurrent GetCpuUsagePercent calls don't crash under the snapshot lock.
/// </summary>
public class ProcessResourceMonitorBranchTests
{
    // ── SampleGpu shape ──────────────────────────────────────────────────────

    [Fact]
    public async Task SampleGpu_returns_empty_list_without_vendor_sampler()
    {
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sut.SampleGpuAsync();

        samples.Should().BeEmpty();
    }

    [Fact]
    public async Task SampleGpu_logs_unsupported_warning_exactly_once()
    {
        Mock<ILogger<ProcessResourceMonitor>> logger = new();
        ProcessResourceMonitor sut = new(logger: logger.Object);

        await sut.SampleGpuAsync();
        await sut.SampleGpuAsync();
        await sut.SampleGpuAsync();

        // The warning is gated by _gpuWarningLogged — three calls, ONE log.
        logger.Verify(
            expression: l =>
                l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task SampleGpu_with_null_logger_does_not_throw()
    {
        // Constructor default: ILogger? null — the warning path uses ?.LogWarning
        // so it must be a no-op when no logger is wired.
        ProcessResourceMonitor sut = new();

        Func<Task> act = async () => await sut.SampleGpuAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NullResourceMonitor_SampleGpu_returns_empty_list()
    {
        NullResourceMonitor sut = new();
        (await sut.SampleGpuAsync()).Should().BeEmpty();
    }

    // ── Concurrent GetCpuUsagePercent ────────────────────────────────────────

    [Fact]
    public async Task GetCpuUsagePercent_concurrent_calls_do_not_throw()
    {
        // The internal _snapshotLock guards the snapshot/cpuTime fields against
        // torn reads under concurrent calls. Hammer it from multiple threads.
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        Task[] tasks = Enumerable
            .Range(start: 0, count: 16)
            .Select(selector: _ =>
                Task.Run(action: () =>
                {
                    for (int i = 0; i < 100; i++)
                        sut.GetCpuUsagePercent();
                })
            )
            .ToArray();

        Func<Task> act = () => Task.WhenAll(tasks: tasks);
        await act.Should().NotThrowAsync();
    }

    // ── GetCpuUsagePercent boundary ─────────────────────────────────────────

    [Fact]
    public void GetCpuUsagePercent_back_to_back_returns_clamped_value()
    {
        // Two back-to-back calls with sub-ms elapsed should NOT yield NaN /
        // negative — the elapsedMs < 1 guard returns 0.
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        sut.GetCpuUsagePercent();
        double second = sut.GetCpuUsagePercent();

        second.Should().BeGreaterThanOrEqualTo(expected: 0);
        second.Should().BeLessThanOrEqualTo(expected: 100);
        double.IsNaN(d: second).Should().BeFalse();
    }

    // ── Baseline isolation between GetCpuUsagePercent and SampleProcessFamilyCpu ──
    //
    // Both samplers used to read/write the SAME _lastCpuTime field under
    // DIFFERENT locks (_snapshotLock vs _systemSnapshotLock), so calling one
    // clobbered the other's baseline — e.g. GetSystemCpuUsagePercent falling
    // back to SampleProcessFamilyCpu on macOS would corrupt whatever
    // GetCpuUsagePercent had primed, and vice versa.

    [Fact]
    public void SampleProcessFamilyCpu_DoesNotClobber_GetCpuUsagePercentBaseline()
    {
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        sut.GetCpuUsagePercent();
        TimeSpan baselineAfterOwnCall = GetPrivateField<TimeSpan>(instance: sut, fieldName: "_lastCpuTime");

        // Burn a little CPU so SampleProcessFamilyCpu's own reading has moved,
        // then call it — pre-fix this call wrote into _lastCpuTime too.
        for (int i = 0; i < 10_000; i++)
            _ = Math.Sqrt(d: i);
        sut.SampleProcessFamilyCpu();
        sut.SampleProcessFamilyCpu();

        TimeSpan baselineAfterFamilySample = GetPrivateField<TimeSpan>(instance: sut, fieldName: "_lastCpuTime");

        baselineAfterFamilySample.Should().Be(expected: baselineAfterOwnCall);
    }

    [Fact]
    public void GetCpuUsagePercent_DoesNotClobber_SampleProcessFamilyCpuBaseline()
    {
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        sut.SampleProcessFamilyCpu();
        TimeSpan baselineAfterOwnCall = GetPrivateField<TimeSpan>(instance: sut, fieldName: "_lastProcessFamilyCpuTime");

        for (int i = 0; i < 10_000; i++)
            _ = Math.Sqrt(d: i);
        sut.GetCpuUsagePercent();
        sut.GetCpuUsagePercent();

        TimeSpan baselineAfterCpuUsageCalls = GetPrivateField<TimeSpan>(
            instance: sut,
            fieldName: "_lastProcessFamilyCpuTime"
        );

        baselineAfterCpuUsageCalls.Should().Be(expected: baselineAfterOwnCall);
    }

    [Fact]
    public async Task Interleaved_GetCpuUsagePercent_and_SampleProcessFamilyCpu_StayIndependent()
    {
        // Hammer both samplers from multiple threads — neither baseline field
        // should ever throw, and each converges on its own last-seen CPU time.
        ProcessResourceMonitor sut = new(logger: NullLogger<ProcessResourceMonitor>.Instance);

        Task cpuUsageLoop = Task.Run(action: () =>
        {
            for (int i = 0; i < 200; i++)
                sut.GetCpuUsagePercent();
        });
        Task familyLoop = Task.Run(action: () =>
        {
            for (int i = 0; i < 200; i++)
                sut.SampleProcessFamilyCpu();
        });

        Func<Task> act = () => Task.WhenAll(tasks: [cpuUsageLoop, familyLoop]);

        await act.Should().NotThrowAsync();
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance
            .GetType()
            .GetField(name: fieldName, bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull(because: $"field {fieldName} should exist on {instance.GetType().Name}");
        return (T)field!.GetValue(obj: instance)!;
    }
}
