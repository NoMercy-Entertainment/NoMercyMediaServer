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
using Moq;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Resources;

namespace NoMercy.Tests.Encoder.Hardware;

public class NvmlGpuSamplerTests
{
    private static ProcessResult SuccessResult(string stdOut) =>
        new(ExitCode: 0, StdOut: stdOut, StdErr: "", Duration: TimeSpan.Zero);

    private static ProcessResult FailureResult() =>
        new(ExitCode: 1, StdOut: "", StdErr: "nvidia-smi not found", Duration: TimeSpan.Zero);

    // ------------------------------------------------------------------
    // Graceful degradation — nvidia-smi absent or failing
    // ------------------------------------------------------------------

    [Fact]
    public async Task SampleGpu_returns_empty_when_nvidia_smi_exits_nonzero()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(FailureResult());

        NvmlGpuSampler sampler = new(runner.Object, NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sampler.SampleGpuAsync();

        await Task.CompletedTask;
        samples.Should().BeEmpty();
    }

    [Fact]
    public async Task SampleGpu_returns_empty_when_nvidia_smi_throws()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new("nvidia-smi not on PATH"));

        NvmlGpuSampler sampler = new(runner.Object, NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sampler.SampleGpuAsync();

        await Task.CompletedTask;
        samples.Should().BeEmpty();
    }

    [Fact]
    public async Task SampleGpu_returns_empty_when_stdout_is_blank()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SuccessResult("   "));

        NvmlGpuSampler sampler = new(runner.Object, NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sampler.SampleGpuAsync();

        await Task.CompletedTask;
        samples.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Parse known nvidia-smi output
    // ------------------------------------------------------------------

    [Fact]
    public void ParseNvidiaSmiOutput_single_process_returns_correct_sample()
    {
        string output = "12345, 512";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(output);

        samples.Should().HaveCount(1);
        samples[0].Pid.Should().Be(12345);
        samples[0].EncoderMemoryBytes.Should().Be(512L * 1024 * 1024);
        samples[0].GpuIndex.Should().Be(0);
        samples[0].EncoderUtilizationPercent.Should().Be(0);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_multiple_processes_returns_all()
    {
        string output = "1001, 256\n2002, 1024\n3003, 128";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(output);

        samples.Should().HaveCount(3);
        samples[0].Pid.Should().Be(1001);
        samples[1].Pid.Should().Be(2002);
        samples[2].Pid.Should().Be(3003);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_memory_converted_from_mb_to_bytes()
    {
        string output = "9999, 1";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(output);

        samples[0].EncoderMemoryBytes.Should().Be(1L * 1024 * 1024);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_skips_malformed_lines()
    {
        string output = "not_a_pid, 512\n1234, not_a_number\n5678, 100";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(output);

        samples.Should().HaveCount(1);
        samples[0].Pid.Should().Be(5678);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_empty_string_returns_empty()
    {
        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput("");

        samples.Should().BeEmpty();
    }

    [Fact]
    public void ParseNvidiaSmiOutput_lines_with_insufficient_fields_are_skipped()
    {
        string output = "12345\n5678, 64";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(output);

        samples.Should().HaveCount(1);
        samples[0].Pid.Should().Be(5678);
    }

    // ------------------------------------------------------------------
    // Throttle: cached result returned within the minimum interval
    // ------------------------------------------------------------------

    [Fact]
    public async Task SampleGpu_caches_result_within_minimum_interval()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(SuccessResult("1234, 512"));

        NvmlGpuSampler sampler = new(runner.Object, NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> first = await sampler.SampleGpuAsync();
        IReadOnlyList<GpuProcessSample> second = await sampler.SampleGpuAsync();

        runner.Verify(
            r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        second.Should().BeEquivalentTo(first);
    }
}
