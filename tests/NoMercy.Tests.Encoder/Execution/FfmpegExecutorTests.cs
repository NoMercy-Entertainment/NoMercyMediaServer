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
        _executor = new(processRunner: _processRunner.Object, logger: NullLogger<FfmpegExecutor>.Instance);
    }

    [Fact]
    public async Task SuccessfulExecution_ReturnsSuccess()
    {
        SetupSuccess();
        FfmpegCommand cmd = BuildSimpleCommand();

        ExecutionResult result = await _executor.ExecuteAsync(command: cmd, inputDuration: TimeSpan.FromMinutes(minutes: 1));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(expected: 0);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task FailedExecution_ReturnsErrorWithClassification()
    {
        _processRunner
            .Setup(expression: r =>
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
                value: new ProcessResult(
                    ExitCode: 1,
                    StdOut: "",
                    StdErr: "No such file or directory: /input.mkv",
                    Duration: TimeSpan.FromSeconds(seconds: 1)
                )
            );

        FfmpegCommand cmd = BuildSimpleCommand();
        ExecutionResult result = await _executor.ExecuteAsync(command: cmd, inputDuration: TimeSpan.FromMinutes(minutes: 1));

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(expected: EncodingErrorKind.InputNotFound);
    }

    [Fact]
    public async Task ProgressCallback_Invoked()
    {
        _processRunner
            .Setup(expression: r =>
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
                action: (exe, args, onStdOut, onStdErr, dir, ct, kill, onStarted) =>
                {
                    onStdOut?.Invoke(obj: "frame=100");
                    onStdOut?.Invoke(obj: "fps=30.0");
                    onStdOut?.Invoke(obj: "out_time_us=30000000");
                    onStdOut?.Invoke(obj: "speed=2.0x");
                    onStdOut?.Invoke(obj: "progress=continue");
                    onStdOut?.Invoke(obj: "frame=200");
                    onStdOut?.Invoke(obj: "fps=30.0");
                    onStdOut?.Invoke(obj: "out_time_us=60000000");
                    onStdOut?.Invoke(obj: "speed=2.0x");
                    onStdOut?.Invoke(obj: "progress=end");
                }
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 30)));

        List<EncodingProgress> progressEvents = [];
        FfmpegCommand cmd = BuildSimpleCommand();

        await _executor.ExecuteAsync(
            command: cmd,
            inputDuration: TimeSpan.FromMinutes(minutes: 1),
            onProgress: p => progressEvents.Add(item: p)
        );

        progressEvents.Should().NotBeEmpty();
        progressEvents.Last().PercentComplete.Should().BeGreaterThan(expected: 0);
    }

    [Fact]
    public async Task DiskFullError_Classified()
    {
        _processRunner
            .Setup(expression: r =>
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
                value: new ProcessResult(ExitCode: 1, StdOut: "", StdErr: "Error: No space left on device", Duration: TimeSpan.FromSeconds(seconds: 1))
            );

        ExecutionResult result = await _executor.ExecuteAsync(
            command: BuildSimpleCommand(),
            inputDuration: TimeSpan.FromMinutes(minutes: 1)
        );

        result.Error!.Kind.Should().Be(expected: EncodingErrorKind.DiskFull);
    }

    private void SetupSuccess()
    {
        _processRunner
            .Setup(expression: r =>
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
            .ReturnsAsync(value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "", Duration: TimeSpan.FromSeconds(seconds: 10)));
    }

    private static FfmpegCommand BuildSimpleCommand()
    {
        return new(Executable: "ffmpeg", Arguments: ["-i", "/input.mkv", "/output.mp4"], WorkingDirectory: null);
    }
}
