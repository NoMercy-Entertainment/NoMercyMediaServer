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
using NoMercy.Encoder.Distribution;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Progress;

namespace NoMercy.Tests.Encoder.Distribution;

/// <summary>
/// Branch-coverage gaps for <see cref="LocalWorkerDispatcher"/> beyond the
/// happy-path / sequencing / cancellation tests in
/// <see cref="LocalWorkerDispatcherTests"/>:
///
/// • Executor throws (vs returning ExecutionResult.Success=false) — the
///   generic Exception catch must populate Error with ex.Message and
///   continue with remaining tasks.
/// • Progress callback wired to sink — each progress tick gets forwarded
///   to ITaskProgressSink.Report with the right taskId.
/// • Progress-sink failure is swallowed — progress reporting MUST NEVER
///   fail the encode (logger debug only).
/// • ErrorMessage formatting — "{Kind}: {Message}" pinned.
/// • TimeRangeDuration null vs set — passed through to executor.
/// </summary>
public class LocalWorkerDispatcherBranchTests
{
    // ── Executor throws ──────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_executor_throws_captures_error_and_continues()
    {
        Mock<IFfmpegExecutor> executor = new();
        int call = 0;
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, _, _, _, _) =>
                {
                    int current = ++call;
                    if (current == 1)
                        throw new InvalidOperationException(message: "disk unmounted mid-encode");
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask[] tasks = [MakeTask(id: "t0", outputPath: "/out/a"), MakeTask(id: "t1", outputPath: "/out/b")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks: tasks, ct: CancellationToken.None);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain(expected: "disk unmounted");
        results[0].Duration.Should().Be(expected: TimeSpan.Zero);
        results[1].Success.Should().BeTrue();
    }

    // ── Progress forwarding ──────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_executor_progress_is_forwarded_to_sink_with_taskId()
    {
        Mock<ITaskProgressSink> sink = new();
        List<(string TaskId, EncodingProgress Progress)> reports = [];
        sink.Setup(expression: s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Callback<string, EncodingProgress>(action: (id, p) => reports.Add(item: (id, p)));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, _, onProgress, _, _) =>
                {
                    // Simulate ffmpeg emitting two progress ticks.
                    onProgress?.Invoke(obj: MakeProgress(fps: 24));
                    onProgress?.Invoke(obj: MakeProgress(fps: 30));
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            progressSink: sink.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(tasks: [MakeTask(id: "t-progress", outputPath: "/out/a")], ct: CancellationToken.None);

        reports.Should().HaveCount(expected: 2);
        reports[index: 0].TaskId.Should().Be(expected: "t-progress");
        reports[index: 0].Progress.CurrentFps.Should().Be(expected: 24);
        reports[index: 1].TaskId.Should().Be(expected: "t-progress");
        reports[index: 1].Progress.CurrentFps.Should().Be(expected: 30);
    }

    [Fact]
    public async Task DispatchAsync_progress_sink_throws_is_swallowed()
    {
        // ITaskProgressSink failures must NEVER fail the encode — they're
        // best-effort observability.
        Mock<ITaskProgressSink> sink = new();
        sink.Setup(expression: s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Throws(exception: new HttpRequestException(message: "coordinator unreachable"));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, _, onProgress, _, _) =>
                {
                    onProgress?.Invoke(obj: MakeProgress(fps: 30));
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            progressSink: sink.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "t0", outputPath: "/out/a")],
            ct: CancellationToken.None
        );

        // Encode still reports success even though the progress sink threw.
        results[0].Success.Should().BeTrue();
    }

    // ── Per-task progress isolation ──────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_per_task_taskId_does_not_close_over_loop_variable()
    {
        // Each task's onProgress callback captures its OWN TaskId — pin so the
        // closure-over-loop-variable bug never returns.
        Mock<ITaskProgressSink> sink = new();
        List<(string TaskId, EncodingProgress Progress)> reports = [];
        sink.Setup(expression: s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Callback<string, EncodingProgress>(action: (id, p) => reports.Add(item: (id, p)));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, _, onProgress, corrId, _) =>
                {
                    onProgress?.Invoke(obj: MakeProgress(corrId: corrId ?? ""));
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor: executor.Object,
            progressSink: sink.Object,
            logger: NullLogger<LocalWorkerDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "alpha", outputPath: "/out/a"), MakeTask(id: "beta", outputPath: "/out/b"), MakeTask(id: "gamma", outputPath: "/out/c")],
            ct: CancellationToken.None
        );

        reports.Select(selector: r => r.TaskId).Should().Equal(expected: ["alpha", "beta", "gamma"]);
    }

    // ── Error message formatting ────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_failure_with_encoding_error_formats_as_kind_colon_message()
    {
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "no such file",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: new(
                        Kind: EncodingErrorKind.InputNotFound,
                        Message: "input.mkv vanished",
                        FfmpegStderr: null,
                        StageName: "Execute",
                        Recoverable: false
                    )
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "t0", outputPath: "/out/a")],
            ct: CancellationToken.None
        );

        results[0].Error.Should().Be(expected: "InputNotFound: input.mkv vanished");
    }

    [Fact]
    public async Task DispatchAsync_failure_with_null_encoding_error_yields_null_error_message()
    {
        // Failure without an EncodingError — error message stays null per
        // ErrorMessage helper's null check.
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(seconds: 1),
                    Error: null
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        DispatchResult[] results = await dispatcher.DispatchAsync(
            tasks: [MakeTask(id: "t0", outputPath: "/out/a")],
            ct: CancellationToken.None
        );

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().BeNull();
    }

    // ── TimeRange propagation ───────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_passes_TimeRangeDuration_to_executor()
    {
        TimeSpan capturedDuration = TimeSpan.MinValue;
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, dur, _, _, _) =>
                {
                    capturedDuration = dur;
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        EncodeTask task = new(
            TaskId: "t0",
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: "/out/a",
            Type: EncodeTaskType.TimeChunk,
            TimeRangeStart: TimeSpan.FromMinutes(minutes: 5),
            TimeRangeDuration: TimeSpan.FromMinutes(minutes: 2)
        );

        await dispatcher.DispatchAsync(tasks: [task], ct: CancellationToken.None);

        capturedDuration.Should().Be(expected: TimeSpan.FromMinutes(minutes: 2));
    }

    [Fact]
    public async Task DispatchAsync_null_TimeRangeDuration_passes_zero_to_executor()
    {
        TimeSpan capturedDuration = TimeSpan.MinValue;
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                FfmpegCommand,
                TimeSpan,
                Action<EncodingProgress>?,
                string?,
                CancellationToken
            >(
                valueFunction: (_, dur, _, _, _) =>
                {
                    capturedDuration = dur;
                    return Task.FromResult(
                        result: new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(seconds: 1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);
        await dispatcher.DispatchAsync(tasks: [MakeTask(id: "t0", outputPath: "/out/a")], ct: CancellationToken.None);

        capturedDuration.Should().Be(expected: TimeSpan.Zero);
    }

    // ── OperationCanceledException propagation ──────────────────────────────

    [Fact]
    public async Task DispatchAsync_OperationCanceledException_from_executor_propagates()
    {
        // OperationCanceledException is explicitly re-thrown — the generic
        // catch arm does NOT swallow cancellation as a task failure.
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(expression: e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: new OperationCanceledException());

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor: executor.Object);

        Func<Task> act = () =>
            dispatcher.DispatchAsync(tasks: [MakeTask(id: "t0", outputPath: "/out/a")], ct: CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LocalWorkerDispatcher NewDispatcher(IFfmpegExecutor executor) =>
        new(executor: executor, logger: NullLogger<LocalWorkerDispatcher>.Instance);

    private static EncodingProgress MakeProgress(double fps = 0, string corrId = "x") =>
        new(
            CorrelationId: corrId,
            PercentComplete: 0,
            Elapsed: TimeSpan.Zero,
            EstimatedRemaining: null,
            CurrentFps: fps,
            CurrentSpeed: 1.0,
            CurrentStage: "Execute",
            CurrentOperation: null,
            BitrateKbps: null,
            Bitrate: "N/A",
            ProcessId: 0,
            CurrentTimeSeconds: 0,
            DurationSeconds: 0
        );

    private static EncodeTask MakeTask(string id, string outputPath) =>
        new(
            TaskId: id,
            Command: new(Executable: "ffmpeg", Arguments: ["-i", "in.mkv", "out.ts"], WorkingDirectory: null),
            OutputPath: outputPath,
            Type: EncodeTaskType.QualityVariant
        );
}
