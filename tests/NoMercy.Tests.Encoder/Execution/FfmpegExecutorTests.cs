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
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Execution;

public class FfmpegExecutorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly FfmpegExecutor _executor;

    public FfmpegExecutorTests()
    {
        _executor = new(_processRunner.Object, NullLogger<FfmpegExecutor>.Instance);
    }

    [Fact]
    public async Task SuccessfulExecution_ReturnsSuccess()
    {
        SetupSuccess();
        FfmpegCommand cmd = BuildSimpleCommand();

        ExecutionResult result = await _executor.ExecuteAsync(cmd, TimeSpan.FromMinutes(1));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task FailedExecution_ReturnsErrorWithClassification()
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<int>?>()
                )
            )
            .ReturnsAsync(
                new ProcessResult(
                    1,
                    "",
                    "No such file or directory: /input.mkv",
                    TimeSpan.FromSeconds(1)
                )
            );

        FfmpegCommand cmd = BuildSimpleCommand();
        ExecutionResult result = await _executor.ExecuteAsync(cmd, TimeSpan.FromMinutes(1));

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public async Task ProgressCallback_Invoked()
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<int>?>()
                )
            )
            .Callback<
                string,
                string[],
                Action<string>?,
                Action<string>?,
                string?,
                CancellationToken,
                CancellationToken,
                Action<int>?
            >(
                (exe, args, onStdOut, onStdErr, dir, ct, kill, onStarted) =>
                {
                    onStdOut?.Invoke("frame=100");
                    onStdOut?.Invoke("fps=30.0");
                    onStdOut?.Invoke("out_time_us=30000000");
                    onStdOut?.Invoke("speed=2.0x");
                    onStdOut?.Invoke("progress=continue");
                    onStdOut?.Invoke("frame=200");
                    onStdOut?.Invoke("fps=30.0");
                    onStdOut?.Invoke("out_time_us=60000000");
                    onStdOut?.Invoke("speed=2.0x");
                    onStdOut?.Invoke("progress=end");
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(30)));

        List<EncodingProgress> progressEvents = [];
        FfmpegCommand cmd = BuildSimpleCommand();

        await _executor.ExecuteAsync(
            cmd,
            TimeSpan.FromMinutes(1),
            onProgress: p => progressEvents.Add(p)
        );

        progressEvents.Should().NotBeEmpty();
        progressEvents.Last().PercentComplete.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DiskFullError_Classified()
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<int>?>()
                )
            )
            .ReturnsAsync(
                new ProcessResult(1, "", "Error: No space left on device", TimeSpan.FromSeconds(1))
            );

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Error!.Kind.Should().Be(EncodingErrorKind.DiskFull);
    }

    private void SetupSuccess()
    {
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Action<int>?>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(10)));
    }

    private static FfmpegCommand BuildSimpleCommand()
    {
        return new("ffmpeg", ["-i", "/input.mkv", "/output.mp4"], null);
    }
}
