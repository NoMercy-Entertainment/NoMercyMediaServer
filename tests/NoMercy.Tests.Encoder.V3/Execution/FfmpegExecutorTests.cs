namespace NoMercy.Tests.Encoder.V3.Execution;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Progress;

public class FfmpegExecutorTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly FfmpegExecutor _executor;

    public FfmpegExecutorTests()
    {
        _executor = new FfmpegExecutor(_processRunner.Object, NullLogger<FfmpegExecutor>.Instance);
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
                    It.IsAny<CancellationToken>()
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
        // Setup process runner that simulates progress output via the onStdOut callback
        _processRunner
            .Setup(r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<Action<string>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                string,
                string[],
                Action<string>?,
                Action<string>?,
                string?,
                CancellationToken
            >(
                (exe, args, onStdOut, onStdErr, dir, ct) =>
                {
                    // Simulate FFmpeg progress output
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

        // Should have at least the end event (throttle may skip the first continue)
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
                    It.IsAny<CancellationToken>()
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
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(10)));
    }

    private static FfmpegCommand BuildSimpleCommand()
    {
        return new FfmpegCommand("ffmpeg", ["-i", "/input.mkv", "/output.mp4"], null);
    }
}
