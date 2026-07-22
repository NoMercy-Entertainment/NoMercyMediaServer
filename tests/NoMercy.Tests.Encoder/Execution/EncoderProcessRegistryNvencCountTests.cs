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

using NoMercy.Encoder.Execution;

namespace NoMercy.Tests.Encoder.Execution;

public class EncoderProcessRegistryNvencCountTests
{
    private static EncoderProcessRegistry MakeRegistry() => new();

    [Fact]
    public void CountConcurrentNvencSessions_returns_zero_initially()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.CountConcurrentNvencSessions().Should().Be(expected: 0);
    }

    [Fact]
    public void CountConcurrentNvencSessions_includes_h264_nvenc_processes()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.RegisterWithArgv(
            jobId: 1,
            processId: 101,
            argv: ["-i", "input.mkv", "-c:v", "h264_nvenc", "out.mp4"]
        );

        registry.CountConcurrentNvencSessions().Should().Be(expected: 1);
    }

    [Fact]
    public void CountConcurrentNvencSessions_includes_hevc_nvenc_av1_nvenc()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.RegisterWithArgv(jobId: 1, processId: 101, argv: ["-c:v", "hevc_nvenc"]);

        registry.RegisterWithArgv(jobId: 2, processId: 102, argv: ["-c:v", "av1_nvenc"]);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 2);
    }

    [Fact]
    public void CountConcurrentNvencSessions_excludes_libx264_processes()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.RegisterWithArgv(
            jobId: 1,
            processId: 101,
            argv: ["-i", "input.mkv", "-c:v", "libx264", "out.mp4"]
        );

        registry.CountConcurrentNvencSessions().Should().Be(expected: 0);
    }

    [Fact]
    public void CountConcurrentNvencSessions_decrements_when_process_unregisters()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.RegisterWithArgv(jobId: 1, processId: 101, argv: ["-c:v", "h264_nvenc"]);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 1);

        registry.Unregister(jobId: 1, processId: 101);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 0);
    }

    [Fact]
    public void CountConcurrentNvencSessions_decrements_when_job_unregisters()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        registry.RegisterWithArgv(jobId: 1, processId: 101, argv: ["-c:v", "hevc_nvenc"]);

        registry.RegisterWithArgv(jobId: 1, processId: 102, argv: ["-c:v", "hevc_nvenc"]);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 2);

        registry.UnregisterJob(jobId: 1);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 0);
    }

    [Fact]
    public void CountConcurrentNvencSessions_ignores_pid_registered_without_argv()
    {
        EncoderProcessRegistry registry = MakeRegistry();

        // Registered via the arg-less overload (e.g. from EventBusProgressObserver)
        // — not counted because argv is unknown
        registry.Register(jobId: 1, processId: 101);

        registry.CountConcurrentNvencSessions().Should().Be(expected: 0);
    }
}
