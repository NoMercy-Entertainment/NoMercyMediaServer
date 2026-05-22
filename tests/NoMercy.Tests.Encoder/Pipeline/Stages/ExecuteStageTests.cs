using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Pipeline.Stages;

/// <summary>
/// ExecuteStage runs ffmpeg commands in order. The fatal-vs-postprocess
/// contract is critical:
///   - Command 0 is the main encode. Failure must abort the whole stage.
///   - Commands 1..N are post-processing (subtitles, fonts, thumbnails).
///     Their failure is logged but non-fatal — the encode produced valid
///     primary output and we shouldn't lose it for a missing fonts.json.
/// </summary>
public class ExecuteStageTests
{
    private static FfmpegCommand Cmd(string name = "encode") =>
        new(Executable: "ffmpeg", Arguments: ["-i", name, "-y", "/out"], WorkingDirectory: null);

    private static ExecutionResult Success() => new(true, 0, "", TimeSpan.Zero, null);

    private static ExecutionResult Failure(string stderr) =>
        new(
            false,
            1,
            stderr,
            TimeSpan.Zero,
            new EncodingError(EncodingErrorKind.Unknown, "exec failed", stderr, "exec", false)
        );

    private static ExecuteStage BuildStage(IFfmpegExecutor executor) =>
        new(executor, NullLogger<ExecuteStage>.Instance);

    private static EncodingContext Ctx() => new(CorrelationId: "ctx-1");

    // ── Success path ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SingleCommand_Success_ReturnsResults()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Success());

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new([Cmd()], InputDuration: TimeSpan.FromMinutes(10));

        StageResult result = await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        result.Should().BeOfType<StageSuccess<ExecutionResult[]>>();
        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().ContainSingle().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSuccessfulCommands_AllRun()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Success());

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new(
            [Cmd("main"), Cmd("subs"), Cmd("fonts")],
            InputDuration: TimeSpan.Zero
        );

        StageResult result = await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().HaveCount(3);
        exec.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }

    // ── Fatal-vs-postprocess contract ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_ReturnsStageFailure()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Failure("ffmpeg blew up"));

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new([Cmd("main"), Cmd("subs")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Message.Should().Be("exec failed");
        // Second command must NOT run after main failure.
        exec.Verify(
            e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_PostProcessFails_StageStillSucceeds()
    {
        // Main encode succeeds, subs extraction fails — encode result must
        // still be reported as success since the primary output is intact.
        Mock<IFfmpegExecutor> exec = new();
        exec.SetupSequence(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Success())
            .ReturnsAsync(Failure("subtitle extraction failed"));

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new([Cmd("main"), Cmd("subs")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        result.Should().BeOfType<StageSuccess<ExecutionResult[]>>();
        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().HaveCount(2);
        success.Value[0].Success.Should().BeTrue();
        success.Value[1].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFailsWithNullError_SynthesizesError()
    {
        // Executor returned Success=false but no error object — stage must
        // still produce a stage failure with a synthesized error.
        ExecutionResult noErrorFailure = new(
            Success: false,
            ExitCode: 137,
            StdErr: "SIGKILL",
            Duration: TimeSpan.Zero,
            Error: null
        );
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(noErrorFailure);

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new([Cmd("main")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        StageFailure failure = result.Should().BeOfType<StageFailure>().Subject;
        failure.Error.Kind.Should().Be(EncodingErrorKind.ProcessCrashed);
        failure.Error.FfmpegStderr.Should().Be("SIGKILL");
        failure.Error.StageName.Should().Be("Execute");
        failure.Error.Recoverable.Should().BeTrue();
    }

    // ── Progress wiring ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_OnlyMainCommandGetsProgressObserver()
    {
        // Index 0 = main encode, gets onProgress callback.
        // Index 1+ = post-processing, gets onProgress = null.
        bool? firstHadProgress = null;
        bool? secondHadProgress = null;
        Mock<IFfmpegExecutor> exec = new();
        int callIndex = 0;
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    if (callIndex == 0)
                        firstHadProgress = onProgress is not null;
                    else if (callIndex == 1)
                        secondHadProgress = onProgress is not null;
                    callIndex++;
                    return Success();
                }
            );

        Mock<IProgressObserver> progress = new();
        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new(
            [Cmd("main"), Cmd("post")],
            InputDuration: TimeSpan.Zero,
            Progress: progress.Object
        );

        await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        firstHadProgress.Should().BeTrue();
        secondHadProgress.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_NoProgressObserver_NoCallbackWired()
    {
        // input.Progress is null → onProgress is always null even for cmd 0.
        Action<EncodingProgress>? observedCallback = null;
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    observedCallback = onProgress;
                    return Success();
                }
            );

        ExecuteStage stage = BuildStage(exec.Object);
        ExecuteInput input = new([Cmd()], InputDuration: TimeSpan.Zero, Progress: null);

        await stage.ExecuteAsync(input, Ctx(), CancellationToken.None);

        observedCallback.Should().BeNull();
    }
}
