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
            .Setup(e =>
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
                (_, _, _, _, _) =>
                {
                    int current = ++call;
                    if (current == 1)
                        throw new InvalidOperationException("disk unmounted mid-encode");
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask[] tasks = [MakeTask("t0", "/out/a"), MakeTask("t1", "/out/b")];

        DispatchResult[] results = await dispatcher.DispatchAsync(tasks, CancellationToken.None);

        results[0].Success.Should().BeFalse();
        results[0].Error.Should().Contain("disk unmounted");
        results[0].Duration.Should().Be(TimeSpan.Zero);
        results[1].Success.Should().BeTrue();
    }

    // ── Progress forwarding ──────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_executor_progress_is_forwarded_to_sink_with_taskId()
    {
        Mock<ITaskProgressSink> sink = new();
        List<(string TaskId, EncodingProgress Progress)> reports = [];
        sink.Setup(s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Callback<string, EncodingProgress>((id, p) => reports.Add((id, p)));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
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
                (_, _, onProgress, _, _) =>
                {
                    // Simulate ffmpeg emitting two progress ticks.
                    onProgress?.Invoke(MakeProgress(fps: 24));
                    onProgress?.Invoke(MakeProgress(fps: 30));
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            sink.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        await dispatcher.DispatchAsync([MakeTask("t-progress", "/out/a")], CancellationToken.None);

        reports.Should().HaveCount(2);
        reports[0].TaskId.Should().Be("t-progress");
        reports[0].Progress.CurrentFps.Should().Be(24);
        reports[1].TaskId.Should().Be("t-progress");
        reports[1].Progress.CurrentFps.Should().Be(30);
    }

    [Fact]
    public async Task DispatchAsync_progress_sink_throws_is_swallowed()
    {
        // ITaskProgressSink failures must NEVER fail the encode — they're
        // best-effort observability.
        Mock<ITaskProgressSink> sink = new();
        sink.Setup(s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Throws(new HttpRequestException("coordinator unreachable"));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
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
                (_, _, onProgress, _, _) =>
                {
                    onProgress?.Invoke(MakeProgress(fps: 30));
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            sink.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        DispatchResult[] results = await dispatcher.DispatchAsync(
            [MakeTask("t0", "/out/a")],
            CancellationToken.None
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
        sink.Setup(s => s.Report(It.IsAny<string>(), It.IsAny<EncodingProgress>()))
            .Callback<string, EncodingProgress>((id, p) => reports.Add((id, p)));

        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
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
                (_, _, onProgress, corrId, _) =>
                {
                    onProgress?.Invoke(MakeProgress(corrId: corrId ?? ""));
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = new(
            executor.Object,
            sink.Object,
            NullLogger<LocalWorkerDispatcher>.Instance
        );

        await dispatcher.DispatchAsync(
            [MakeTask("alpha", "/out/a"), MakeTask("beta", "/out/b"), MakeTask("gamma", "/out/c")],
            CancellationToken.None
        );

        reports.Select(r => r.TaskId).Should().Equal("alpha", "beta", "gamma");
    }

    // ── Error message formatting ────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_failure_with_encoding_error_formats_as_kind_colon_message()
    {
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "no such file",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: new(
                        EncodingErrorKind.InputNotFound,
                        "input.mkv vanished",
                        null,
                        "Execute",
                        Recoverable: false
                    )
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        DispatchResult[] results = await dispatcher.DispatchAsync(
            [MakeTask("t0", "/out/a")],
            CancellationToken.None
        );

        results[0].Error.Should().Be("InputNotFound: input.mkv vanished");
    }

    [Fact]
    public async Task DispatchAsync_failure_with_null_encoding_error_yields_null_error_message()
    {
        // Failure without an EncodingError — error message stays null per
        // ErrorMessage helper's null check.
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ExecutionResult(
                    Success: false,
                    ExitCode: 1,
                    StdErr: "",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null
                )
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        DispatchResult[] results = await dispatcher.DispatchAsync(
            [MakeTask("t0", "/out/a")],
            CancellationToken.None
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
            .Setup(e =>
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
                (_, dur, _, _, _) =>
                {
                    capturedDuration = dur;
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        EncodeTask task = new(
            TaskId: "t0",
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: "/out/a",
            Type: EncodeTaskType.TimeChunk,
            TimeRangeStart: TimeSpan.FromMinutes(5),
            TimeRangeDuration: TimeSpan.FromMinutes(2)
        );

        await dispatcher.DispatchAsync([task], CancellationToken.None);

        capturedDuration.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task DispatchAsync_null_TimeRangeDuration_passes_zero_to_executor()
    {
        TimeSpan capturedDuration = TimeSpan.MinValue;
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
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
                (_, dur, _, _, _) =>
                {
                    capturedDuration = dur;
                    return Task.FromResult(
                        new ExecutionResult(
                            Success: true,
                            ExitCode: 0,
                            StdErr: "",
                            Duration: TimeSpan.FromSeconds(1),
                            Error: null
                        )
                    );
                }
            );

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);
        await dispatcher.DispatchAsync([MakeTask("t0", "/out/a")], CancellationToken.None);

        capturedDuration.Should().Be(TimeSpan.Zero);
    }

    // ── OperationCanceledException propagation ──────────────────────────────

    [Fact]
    public async Task DispatchAsync_OperationCanceledException_from_executor_propagates()
    {
        // OperationCanceledException is explicitly re-thrown — the generic
        // catch arm does NOT swallow cancellation as a task failure.
        Mock<IFfmpegExecutor> executor = new();
        executor
            .Setup(e =>
                e.ExecuteAsync(
                    It.IsAny<FfmpegCommand>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Action<EncodingProgress>?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException());

        LocalWorkerDispatcher dispatcher = NewDispatcher(executor.Object);

        Func<Task> act = () =>
            dispatcher.DispatchAsync([MakeTask("t0", "/out/a")], CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LocalWorkerDispatcher NewDispatcher(IFfmpegExecutor executor) =>
        new(executor, NullLogger<LocalWorkerDispatcher>.Instance);

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
            Command: new("ffmpeg", ["-i", "in.mkv", "out.ts"], null),
            OutputPath: outputPath,
            Type: EncodeTaskType.QualityVariant
        );
}
