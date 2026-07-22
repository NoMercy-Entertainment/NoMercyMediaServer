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
            .Setup(expression: r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: FailureResult());

        NvmlGpuSampler sampler = new(processRunner: runner.Object, logger: NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sampler.SampleGpuAsync();

        await Task.CompletedTask;
        samples.Should().BeEmpty();
    }

    [Fact]
    public async Task SampleGpu_returns_empty_when_nvidia_smi_throws()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new(message: "nvidia-smi not on PATH"));

        NvmlGpuSampler sampler = new(processRunner: runner.Object, logger: NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> samples = await sampler.SampleGpuAsync();

        await Task.CompletedTask;
        samples.Should().BeEmpty();
    }

    [Fact]
    public async Task SampleGpu_returns_empty_when_stdout_is_blank()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: SuccessResult(stdOut: "   "));

        NvmlGpuSampler sampler = new(processRunner: runner.Object, logger: NullLogger<NvmlGpuSampler>.Instance);

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

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: output);

        samples.Should().HaveCount(expected: 1);
        samples[index: 0].Pid.Should().Be(expected: 12345);
        samples[index: 0].EncoderMemoryBytes.Should().Be(expected: 512L * 1024 * 1024);
        samples[index: 0].GpuIndex.Should().Be(expected: 0);
        samples[index: 0].EncoderUtilizationPercent.Should().Be(expected: 0);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_multiple_processes_returns_all()
    {
        string output = "1001, 256\n2002, 1024\n3003, 128";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: output);

        samples.Should().HaveCount(expected: 3);
        samples[index: 0].Pid.Should().Be(expected: 1001);
        samples[index: 1].Pid.Should().Be(expected: 2002);
        samples[index: 2].Pid.Should().Be(expected: 3003);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_memory_converted_from_mb_to_bytes()
    {
        string output = "9999, 1";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: output);

        samples[index: 0].EncoderMemoryBytes.Should().Be(expected: 1L * 1024 * 1024);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_skips_malformed_lines()
    {
        string output = "not_a_pid, 512\n1234, not_a_number\n5678, 100";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: output);

        samples.Should().HaveCount(expected: 1);
        samples[index: 0].Pid.Should().Be(expected: 5678);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_empty_string_returns_empty()
    {
        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: "");

        samples.Should().BeEmpty();
    }

    [Fact]
    public void ParseNvidiaSmiOutput_lines_with_insufficient_fields_are_skipped()
    {
        string output = "12345\n5678, 64";

        IReadOnlyList<GpuProcessSample> samples = NvmlGpuSampler.ParseNvidiaSmiOutput(stdOut: output);

        samples.Should().HaveCount(expected: 1);
        samples[index: 0].Pid.Should().Be(expected: 5678);
    }

    // ------------------------------------------------------------------
    // Throttle: cached result returned within the minimum interval
    // ------------------------------------------------------------------

    [Fact]
    public async Task SampleGpu_caches_result_within_minimum_interval()
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: SuccessResult(stdOut: "1234, 512"));

        NvmlGpuSampler sampler = new(processRunner: runner.Object, logger: NullLogger<NvmlGpuSampler>.Instance);

        IReadOnlyList<GpuProcessSample> first = await sampler.SampleGpuAsync();
        IReadOnlyList<GpuProcessSample> second = await sampler.SampleGpuAsync();

        runner.Verify(
            expression: r =>
                r.RunAsync(
                    "nvidia-smi",
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );

        second.Should().BeEquivalentTo(expectation: first);
    }
}
