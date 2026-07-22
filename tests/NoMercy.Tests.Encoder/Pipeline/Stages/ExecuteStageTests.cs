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
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

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

    private static ExecutionResult Success() => new(Success: true, ExitCode: 0, StdErr: "", Duration: TimeSpan.Zero, Error: null);

    private static ExecutionResult Failure(string stderr) =>
        new(
            Success: false,
            ExitCode: 1,
            StdErr: stderr,
            Duration: TimeSpan.Zero,
            Error: new(Kind: EncodingErrorKind.Unknown, Message: "exec failed", FfmpegStderr: stderr, StageName: "exec", Recoverable: false)
        );

    private static ExecuteStage BuildStage(IFfmpegExecutor executor, ICheckpointStore? store = null)
    {
        ICheckpointStore effectiveStore = store ?? new Mock<ICheckpointStore>().Object;
        return new(executor: executor, checkpointStore: effectiveStore, logger: NullLogger<ExecuteStage>.Instance);
    }

    private static EncodingContext Ctx(string? outputDirectory = null) =>
        new(CorrelationId: "ctx-1", OutputDirectory: outputDirectory, InputPath: "/media/src.mkv");

    // ── Success path ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SingleCommand_Success_ReturnsResults()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success());

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(Commands: [Cmd()], InputDuration: TimeSpan.FromMinutes(minutes: 10));

        StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        result.Should().BeOfType<StageSuccess<ExecutionResult[]>>();
        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().ContainSingle().Which.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSuccessfulCommands_AllRun()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success());

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(
            Commands: [Cmd(name: "main"), Cmd(name: "subs"), Cmd(name: "fonts")],
            InputDuration: TimeSpan.Zero
        );

        StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().HaveCount(expected: 3);
        exec.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 3)
        );
    }

    // ── Success path does NOT write checkpoint ─────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Success_DoesNotWriteCheckpoint()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success());

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(executor: exec.Object, store: store.Object);
        ExecuteInput input = new(Commands: [Cmd()], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input: input, context: Ctx(outputDirectory: "/output/dir"), ct: CancellationToken.None);

        store.Verify(
            expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    // ── Fatal-vs-postprocess contract ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_ReturnsStageFailure()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Failure(stderr: "ffmpeg blew up"));

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main"), Cmd(name: "subs")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        result.Should().BeOfType<StageFailure>();
        StageFailure failure = (StageFailure)result;
        failure.Error.Message.Should().Be(expected: "exec failed");
        // Second command must NOT run after main failure.
        exec.Verify(
            expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_PostProcessFails_StageStillSucceeds()
    {
        // Main encode succeeds, subs extraction fails — encode result must
        // still be reported as success since the primary output is intact.
        Mock<IFfmpegExecutor> exec = new();
        exec.SetupSequence(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success())
            .ReturnsAsync(value: Failure(stderr: "subtitle extraction failed"));

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main"), Cmd(name: "subs")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        result.Should().BeOfType<StageSuccess<ExecutionResult[]>>();
        StageSuccess<ExecutionResult[]> success = (StageSuccess<ExecutionResult[]>)result;
        success.Value.Should().HaveCount(expected: 2);
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
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: noErrorFailure);

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main")], InputDuration: TimeSpan.Zero);

        StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        StageFailure failure = result.Should().BeOfType<StageFailure>().Subject;
        failure.Error.Kind.Should().Be(expected: EncodingErrorKind.ProcessCrashed);
        failure.Error.FfmpegStderr.Should().Be(expected: "SIGKILL");
        failure.Error.StageName.Should().Be(expected: "Execute");
        failure.Error.Recoverable.Should().BeTrue();
    }

    // ── Crash checkpoint written on main-command failure ───────────────────

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_WritesCheckpointWithFailedAt()
    {
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Failure(stderr: "out of memory"));

        JobCheckpoint? captured = null;
        Mock<ICheckpointStore> store = new();
        store
            .Setup(expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>(action: (cp, _) => captured = cp)
            .Returns(value: Task.CompletedTask);

        ExecuteStage stage = BuildStage(executor: exec.Object, store: store.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input: input, context: Ctx(outputDirectory: "/output/dir"), ct: CancellationToken.None);

        store.Verify(
            expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            times: Times.Once
        );
        captured.Should().NotBeNull();
        captured!.FailedAt.Should().NotBeNull();
        captured.OutputDirectory.Should().Be(expected: "/output/dir");
        captured.JobId.Should().Be(expected: "ctx-1");
        captured.LastFfmpegStderrTail.Should().Be(expected: "out of memory");
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_CheckpointHasLastProgressMs()
    {
        // When progress events arrive before the crash, LastProgressMs must
        // capture the furthest position the encoder reached.
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (
                    FfmpegCommand _,
                    TimeSpan _,
                    Action<EncodingProgress>? onProgress,
                    string? _,
                    CancellationToken _
                ) =>
                {
                    onProgress?.Invoke(
                        obj: new(
                            CorrelationId: "ctx-1",
                            PercentComplete: 50,
                            Elapsed: TimeSpan.FromSeconds(seconds: 30),
                            EstimatedRemaining: null,
                            CurrentFps: null,
                            CurrentSpeed: null,
                            CurrentStage: null,
                            CurrentOperation: null,
                            CurrentTimeSeconds: 120.5
                        )
                    );
                    return Failure(stderr: "crash after 120s");
                }
            );

        JobCheckpoint? captured = null;
        Mock<ICheckpointStore> store = new();
        store
            .Setup(expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>(action: (cp, _) => captured = cp)
            .Returns(value: Task.CompletedTask);

        ExecuteStage stage = BuildStage(executor: exec.Object, store: store.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input: input, context: Ctx(outputDirectory: "/output/dir"), ct: CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.LastProgressMs.Should().Be(expected: 120_500);
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_NoOutputDirectory_NoCheckpointWritten()
    {
        // When context has no OutputDirectory, crash checkpoint is skipped
        // (no valid key to write under).
        Mock<IFfmpegExecutor> exec = new();
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Failure(stderr: "boom"));

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(executor: exec.Object, store: store.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input: input, context: Ctx(outputDirectory: null), ct: CancellationToken.None);

        store.Verify(
            expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            times: Times.Never
        );
    }

    [Fact]
    public async Task ExecuteAsync_PostProcessCommandFails_NoCheckpointWritten()
    {
        // Only main command (index 0) failure triggers a checkpoint.
        // Post-process failure is non-fatal and must NOT write a checkpoint.
        Mock<IFfmpegExecutor> exec = new();
        exec.SetupSequence(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success())
            .ReturnsAsync(value: Failure(stderr: "font extract failed"));

        Mock<ICheckpointStore> store = new();
        ExecuteStage stage = BuildStage(executor: exec.Object, store: store.Object);
        ExecuteInput input = new(Commands: [Cmd(name: "main"), Cmd(name: "fonts")], InputDuration: TimeSpan.Zero);

        await stage.ExecuteAsync(input: input, context: Ctx(outputDirectory: "/output/dir"), ct: CancellationToken.None);

        store.Verify(
            expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()),
            times: Times.Never
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
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (
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
        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(
            Commands: [Cmd(name: "main"), Cmd(name: "post")],
            InputDuration: TimeSpan.Zero,
            Progress: progress.Object
        );

        await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

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
        exec.Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (
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

        ExecuteStage stage = BuildStage(executor: exec.Object);
        ExecuteInput input = new(Commands: [Cmd()], InputDuration: TimeSpan.Zero, Progress: null);

        await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

        // Stage always wires a progress callback on cmd 0 to track LastProgressMs.
        observedCallback.Should().NotBeNull();
    }

    // ── DRM key temp-directory cleanup ──────────────────────────────────────
    //
    // Aes128HlsDrmProcessor writes drm.key/drm_keyinfo.txt to a per-encode
    // directory under StoragePaths.TempRoot (never the published output dir —
    // see Aes128HlsDrmProcessorTests). ExecuteStage is the last stage that
    // reads that directory (via -hls_key_info_file), so it deletes it once
    // ffmpeg is done — success or failure — closing the temp-file leak.

    [Fact]
    public async Task ExecuteAsync_Success_DeletesDrmKeyTempDirectoryUnderTempRoot()
    {
        string drmTempDir = Path.Combine(
            path1: StoragePaths.TempRoot,
            path2: "drm-keys",
            path3: Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: drmTempDir);
        string keyInfoPath = Path.Combine(path1: drmTempDir, path2: "drm_keyinfo.txt");
        await File.WriteAllTextAsync(path: keyInfoPath, contents: "https://example/key\n/tmp/drm.key\nabcd\n");
        await File.WriteAllBytesAsync(path: Path.Combine(path1: drmTempDir, path2: "drm.key"), bytes: new byte[16]);

        try
        {
            Mock<IFfmpegExecutor> exec = new();
            exec.Setup(expression: e =>
                    e.ExecuteAsync(
                        It.IsAny<FfmpegCommand>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<Action<EncodingProgress>?>(),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: Success());

            ExecuteStage stage = BuildStage(executor: exec.Object);
            FfmpegCommand cmd = new(
                Executable: "ffmpeg",
                Arguments: ["-i", "src.mkv", "-hls_key_info_file", keyInfoPath, "-y", "/out"],
                WorkingDirectory: null
            );
            ExecuteInput input = new(Commands: [cmd], InputDuration: TimeSpan.Zero);

            await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

            Directory
                .Exists(path: drmTempDir)
                .Should()
                .BeFalse(because: "the DRM key temp dir must be deleted once ffmpeg consumes it");
        }
        finally
        {
            if (Directory.Exists(path: drmTempDir))
                Directory.Delete(path: drmTempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MainCommandFails_StillDeletesDrmKeyTempDirectory()
    {
        string drmTempDir = Path.Combine(
            path1: StoragePaths.TempRoot,
            path2: "drm-keys",
            path3: Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: drmTempDir);
        string keyInfoPath = Path.Combine(path1: drmTempDir, path2: "drm_keyinfo.txt");
        await File.WriteAllTextAsync(path: keyInfoPath, contents: "https://example/key\n/tmp/drm.key\nabcd\n");

        try
        {
            Mock<IFfmpegExecutor> exec = new();
            exec.Setup(expression: e =>
                    e.ExecuteAsync(
                        It.IsAny<FfmpegCommand>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<Action<EncodingProgress>?>(),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: Failure(stderr: "ffmpeg blew up"));

            ExecuteStage stage = BuildStage(executor: exec.Object);
            FfmpegCommand cmd = new(
                Executable: "ffmpeg",
                Arguments: ["-i", "src.mkv", "-hls_key_info_file", keyInfoPath, "-y", "/out"],
                WorkingDirectory: null
            );
            ExecuteInput input = new(Commands: [cmd], InputDuration: TimeSpan.Zero);

            StageResult result = await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

            result.Should().BeOfType<StageFailure>();
            Directory
                .Exists(path: drmTempDir)
                .Should()
                .BeFalse(because: "cleanup must run even when the main encode command fails");
        }
        finally
        {
            if (Directory.Exists(path: drmTempDir))
                Directory.Delete(path: drmTempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_KeyInfoFileOutsideTempRoot_IsNotDeleted()
    {
        // Defensive scope check: cleanup only ever touches paths under
        // StoragePaths.TempRoot. A path elsewhere (e.g. a published output
        // dir) must be left alone even if it happens to carry the flag.
        // Uses the current working directory rather than mutating the
        // process-wide StoragePaths.TempRoot static, which other tests read
        // concurrently.
        string outsideDir = Path.Combine(
            path1: Directory.GetCurrentDirectory(),
            path2: $"not-under-temproot-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(path: outsideDir);
        string keyInfoPath = Path.Combine(path1: outsideDir, path2: "drm_keyinfo.txt");
        await File.WriteAllTextAsync(path: keyInfoPath, contents: "https://example/key\n/tmp/drm.key\nabcd\n");

        try
        {
            Path.GetFullPath(path: outsideDir)
                .Should()
                .NotStartWith(
                    unexpected: Path.GetFullPath(path: StoragePaths.TempRoot),
                    because: "test fixture must be outside TempRoot for this assertion to be meaningful"
                );

            Mock<IFfmpegExecutor> exec = new();
            exec.Setup(expression: e =>
                    e.ExecuteAsync(
                        It.IsAny<FfmpegCommand>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<Action<EncodingProgress>?>(),
                        It.IsAny<string?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(value: Success());

            ExecuteStage stage = BuildStage(executor: exec.Object);
            FfmpegCommand cmd = new(
                Executable: "ffmpeg",
                Arguments: ["-i", "src.mkv", "-hls_key_info_file", keyInfoPath, "-y", "/out"],
                WorkingDirectory: null
            );
            ExecuteInput input = new(Commands: [cmd], InputDuration: TimeSpan.Zero);

            await stage.ExecuteAsync(input: input, context: Ctx(), ct: CancellationToken.None);

            Directory
                .Exists(path: outsideDir)
                .Should()
                .BeTrue(because: "cleanup must never delete a directory outside TempRoot");
        }
        finally
        {
            if (Directory.Exists(path: outsideDir))
                Directory.Delete(path: outsideDir, recursive: true);
        }
    }
}
