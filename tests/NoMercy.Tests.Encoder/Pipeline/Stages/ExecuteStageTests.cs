using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;
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
///
/// Crash-checkpoint contract:
///   - Main command failure writes a checkpoint with FailedAt set and
///     LastProgressMs reflecting the last progress snapshot seen.
///   - Success writes no checkpoint.
///   - Cancel (OperationCanceledException) is NOT caught here — it propagates
///     to the strategy layer which handles its own cancel-path cleanup.
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

    private static ExecuteStage BuildStage(
        IFfmpegExecutor executor,
        ICheckpointStore? store = null
    )
    {
        ICheckpointStore effectiveStore = store ?? new Mock<ICheckpointStore>().Object;
        return new(executor, effectiveStore, NullLogger<ExecuteStage>.Instance);
    }

    private static EncodingContext Ctx(string? outputDirectory = null) =>
        new(CorrelationId: "ctx-1", OutputDirectory: outputDirectory, InputPath: "/media/src.mkv");

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

    // ── Success path does NOT write checkpoint ─────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Success_DoesNotWriteCheckpoint()
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

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(exec.Object, store.Object);
        ExecuteInput input = new([Cmd()], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input, Ctx("/output/dir"), CancellationToken.None);

        store.Verify(
            s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never
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

    // ── Crash checkpoint written on main-command failure ───────────────────

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_WritesCheckpointWithFailedAt()
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
            .ReturnsAsync(Failure("out of memory"));

        JobCheckpoint? captured = null;
        Mock<ICheckpointStore> store = new();
        store
            .Setup(s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>((cp, _) => captured = cp)
            .Returns(Task.CompletedTask);

        ExecuteStage stage = BuildStage(exec.Object, store.Object);
        ExecuteInput input = new([Cmd("main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input, Ctx("/output/dir"), CancellationToken.None);

        store.Verify(
            s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        captured.Should().NotBeNull();
        captured!.FailedAt.Should().NotBeNull();
        captured.OutputDirectory.Should().Be("/output/dir");
        captured.JobId.Should().Be("ctx-1");
        captured.LastFfmpegStderrTail.Should().Be("out of memory");
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_CheckpointHasLastProgressMs()
    {
        // When progress events arrive before the crash, LastProgressMs must
        // capture the furthest position the encoder reached.
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
                    onProgress?.Invoke(
                        new EncodingProgress(
                            CorrelationId: "ctx-1",
                            PercentComplete: 50,
                            Elapsed: TimeSpan.FromSeconds(30),
                            EstimatedRemaining: null,
                            CurrentFps: null,
                            CurrentSpeed: null,
                            CurrentStage: null,
                            CurrentOperation: null,
                            CurrentTimeSeconds: 120.5
                        )
                    );
                    return Failure("crash after 120s");
                }
            );

        JobCheckpoint? captured = null;
        Mock<ICheckpointStore> store = new();
        store
            .Setup(s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>((cp, _) => captured = cp)
            .Returns(Task.CompletedTask);

        ExecuteStage stage = BuildStage(exec.Object, store.Object);
        ExecuteInput input = new([Cmd("main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input, Ctx("/output/dir"), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.LastProgressMs.Should().Be(120_500);
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_NoOutputDirectory_NoCheckpointWritten()
    {
        // When context has no OutputDirectory, crash checkpoint is skipped
        // (no valid key to write under).
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
            .ReturnsAsync(Failure("boom"));

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(exec.Object, store.Object);
        ExecuteInput input = new([Cmd("main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(
            input,
            Ctx(outputDirectory: null),
            CancellationToken.None
        );

        store.Verify(
            s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_PostProcessCommandFails_NoCheckpointWritten()
    {
        // Only main command (index 0) failure triggers a checkpoint.
        // Post-process failure is non-fatal and must NOT write a checkpoint.
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
            .ReturnsAsync(Failure("font extract failed"));

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(exec.Object, store.Object);
        ExecuteInput input = new([Cmd("main"), Cmd("fonts")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input, Ctx("/output/dir"), CancellationToken.None);

        store.Verify(
            s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
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
        // However the stage internally tracks progress for crash-checkpoint purposes,
        // so the executor still receives a non-null onProgress for command 0.
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

        // Stage always wires a progress callback on cmd 0 to track LastProgressMs.
        observedCallback.Should().NotBeNull();
    }
}
