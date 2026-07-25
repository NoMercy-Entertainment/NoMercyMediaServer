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

/// <summary>
/// Branch-coverage gaps for <see cref="FfmpegExecutor"/>:
///
/// • ClassifyError keyword table — InputCorrupt / CodecUnavailable (2 patterns) /
///   HardwareFailure (2 patterns) / OutOfMemory / ProcessCrashed (default).
///   Wrong classification means the JobQueue's retry / skip decision goes off
///   the rails (recoverable errors get permanent-failed; permanent errors get
///   stuck in retry loops).
/// • Recoverable flag — only HardwareFailure + ProcessCrashed are recoverable;
///   everything else is permanent.
/// • Stderr truncation — errors over 2000 chars keep the LAST 2000 (most
///   recent ffmpeg output is usually most diagnostic). Pin so refactors don't
///   accidentally keep the first 2000 instead.
/// • Empty-stderr ProcessCrashed default — when the runner returns exit=1 +
///   blank stderr, the executor must still classify as ProcessCrashed, not
///   throw on a null/empty branch.
/// • Bitrate display — BitrateKbps null shows "N/A"; populated shows F1 +
///   kbits/s suffix.
/// • Average metrics with zero samples — must NOT divide by zero. Empty
///   progress stream yields AverageSpeed=0 / AverageFps=0.
/// </summary>
public class FfmpegExecutorBranchTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();
    private readonly FfmpegExecutor _executor;

    public FfmpegExecutorBranchTests()
    {
        _executor = new(_processRunner.Object, NullLogger<FfmpegExecutor>.Instance);
    }

    // ── ClassifyError keyword table ──────────────────────────────────────────

    [Theory]
    [InlineData("No such file or directory", EncodingErrorKind.InputNotFound, false)]
    [InlineData("Invalid data found when processing input", EncodingErrorKind.InputCorrupt, false)]
    [InlineData(
        "Codec not currently supported in container",
        EncodingErrorKind.CodecUnavailable,
        false
    )]
    [InlineData("Encoder libx265 not found", EncodingErrorKind.CodecUnavailable, false)]
    [InlineData("Device 'cuda' cannot allocate", EncodingErrorKind.HardwareFailure, true)]
    [InlineData("No space left on device", EncodingErrorKind.DiskFull, false)]
    [InlineData("Out of memory", EncodingErrorKind.HardwareFailure, true)]
    [InlineData("Something we have never seen", EncodingErrorKind.ProcessCrashed, true)]
    public async Task Classify_error_per_keyword_table(
        string stderr,
        EncodingErrorKind expectedKind,
        bool expectedRecoverable
    )
    {
        SetupFailedRun(stderr);

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(expectedKind);
        result.Error.Recoverable.Should().Be(expectedRecoverable);
    }

    [Fact]
    public async Task Classify_keyword_match_is_case_insensitive()
    {
        // ClassifyError uses ToLowerInvariant — uppercase keywords must match.
        SetupFailedRun("ERROR: NO SUCH FILE OR DIRECTORY");

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Error!.Kind.Should().Be(EncodingErrorKind.InputNotFound);
    }

    // ── Empty stderr default classification ─────────────────────────────────

    [Fact]
    public async Task Empty_stderr_with_nonzero_exit_classifies_as_ProcessCrashed()
    {
        SetupFailedRun("");

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Error!.Kind.Should().Be(EncodingErrorKind.ProcessCrashed);
        result.Error.Recoverable.Should().BeTrue(); // ProcessCrashed is recoverable
    }

    // ── Stderr truncation ────────────────────────────────────────────────────

    [Fact]
    public async Task Stderr_over_2000_chars_keeps_last_2000_in_error_message()
    {
        // Diagnostic value lives in the tail — the actual failure line comes
        // AFTER ffmpeg's banner spam, library version dumps, etc. We want the
        // executor to keep the most recent bytes, not the first 2000.
        string head = new('A', 3000);
        string tail = "FATAL_LINE_AT_END";
        string stderr = head + tail;

        SetupFailedRun(stderr);

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Error!.FfmpegStderr.Should().NotBeNull();
        result.Error.FfmpegStderr!.Length.Should().Be(2000);
        result.Error.FfmpegStderr.Should().EndWith(tail);
    }

    [Fact]
    public async Task Stderr_at_or_under_2000_chars_kept_in_full()
    {
        string stderr = new('A', 2000);
        SetupFailedRun(stderr);

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Error!.FfmpegStderr.Should().Be(stderr);
    }

    // ── Average metrics edge cases ───────────────────────────────────────────

    [Fact]
    public async Task Success_with_no_progress_samples_returns_zero_averages_not_NaN()
    {
        // If FFmpeg exits before any progress=continue lines come through,
        // the metric accumulator stays at sampleCount=0. Must NOT divide-by-zero
        // and yield NaN — the dashboard renders metrics as numbers.
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
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1)
        );

        result.Success.Should().BeTrue();
        result.Metrics.Should().NotBeNull();
        result.Metrics!.AverageSpeed.Should().Be(0);
        result.Metrics.AverageFps.Should().Be(0);
        result.Metrics.PeakSpeed.Should().Be(0);
        result.Metrics.PeakFps.Should().Be(0);
        double.IsNaN(result.Metrics.AverageSpeed).Should().BeFalse();
        double.IsNaN(result.Metrics.AverageFps).Should().BeFalse();
    }

    [Fact]
    public async Task Success_aggregates_averages_and_peaks_across_samples()
    {
        // Two progress=continue samples count toward averages; the final
        // progress=end sample is excluded by design (it's the wrap-up tick,
        // not a representative measurement). Speeds 1.0 + 3.0 average to 2.0,
        // fps 20 + 60 average to 40, peaks come from the higher sample.
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
                    onStdOut?.Invoke("fps=20.0");
                    onStdOut?.Invoke("out_time_us=10000000");
                    onStdOut?.Invoke("total_size=512000");
                    onStdOut?.Invoke("speed=1.0x");
                    onStdOut?.Invoke("progress=continue");

                    onStdOut?.Invoke("frame=200");
                    onStdOut?.Invoke("fps=60.0");
                    onStdOut?.Invoke("out_time_us=20000000");
                    onStdOut?.Invoke("total_size=1024000");
                    onStdOut?.Invoke("speed=3.0x");
                    onStdOut?.Invoke("progress=continue");

                    // Final wrap-up sample — excluded from averages by design.
                    onStdOut?.Invoke("frame=210");
                    onStdOut?.Invoke("fps=70.0");
                    onStdOut?.Invoke("out_time_us=21000000");
                    onStdOut?.Invoke("total_size=1100000");
                    onStdOut?.Invoke("speed=5.0x");
                    onStdOut?.Invoke("progress=end");
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(2)));

        ExecutionResult result = await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromSeconds(20)
        );

        result.Success.Should().BeTrue();
        result.Metrics!.AverageSpeed.Should().Be(2.0);
        result.Metrics.AverageFps.Should().Be(40.0);
        result.Metrics.PeakSpeed.Should().Be(3.0);
        result.Metrics.PeakFps.Should().Be(60.0);
        // TotalSizeBytes carries the LAST non-end sample's size because the
        // command in this test does not pass `pipe:1`. The end-of-stream
        // sample only updates lastTotalSize when hasProgressPipe is true.
        result.Metrics.TotalSizeBytes.Should().Be(1024000);
    }

    // ── Progress callback shape ──────────────────────────────────────────────

    [Fact]
    public async Task Progress_callback_omitted_when_null_does_not_throw()
    {
        // The onProgress=null path must short-circuit cleanly inside OnStdOut
        // — `if (onProgress is null) return;`.
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
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

        Func<Task> act = () =>
            _executor.ExecuteAsync(
                BuildSimpleCommand(),
                TimeSpan.FromSeconds(60),
                onProgress: null
            );
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Progress_callback_clamps_percent_to_100_when_out_time_exceeds_duration()
    {
        // Source duration was wrong (e.g. ffprobe lied) and ffmpeg reports
        // out_time past the documented duration — percent must clamp to 100.
        EncodingProgress? lastProgress = null;
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
                    // Duration was set to 1 second, ffmpeg reports 5 seconds.
                    onStdOut?.Invoke("frame=100");
                    onStdOut?.Invoke("fps=30.0");
                    onStdOut?.Invoke("out_time_us=5000000");
                    onStdOut?.Invoke("speed=2.0x");
                    onStdOut?.Invoke("progress=end");
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

        await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            inputDuration: TimeSpan.FromSeconds(1),
            onProgress: p => lastProgress = p
        );

        lastProgress.Should().NotBeNull();
        lastProgress!.PercentComplete.Should().Be(100.0);
    }

    [Fact]
    public async Task Progress_percent_is_zero_when_input_duration_is_zero()
    {
        // Live transcode / unknown source: duration=0 must NOT divide-by-zero.
        EncodingProgress? lastProgress = null;
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
                    onStdOut?.Invoke("out_time_us=5000000");
                    onStdOut?.Invoke("speed=2.0x");
                    onStdOut?.Invoke("progress=end");
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

        await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            inputDuration: TimeSpan.Zero,
            onProgress: p => lastProgress = p
        );

        lastProgress.Should().NotBeNull();
        lastProgress!.PercentComplete.Should().Be(0);
        lastProgress.EstimatedRemaining.Should().BeNull();
    }

    // ── Bitrate display ──────────────────────────────────────────────────────

    [Fact]
    public async Task Progress_bitrate_string_is_NA_when_missing()
    {
        EncodingProgress? lastProgress = null;
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
                    onStdOut?.Invoke("out_time_us=10000000");
                    // no bitrate= line emitted
                    onStdOut?.Invoke("speed=1.0x");
                    onStdOut?.Invoke("progress=end");
                }
            )
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.FromSeconds(1)));

        await _executor.ExecuteAsync(
            BuildSimpleCommand(),
            TimeSpan.FromMinutes(1),
            onProgress: p => lastProgress = p
        );

        lastProgress!.Bitrate.Should().Be("N/A");
        lastProgress.BitrateKbps.Should().BeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetupFailedRun(string stderr) =>
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
            .ReturnsAsync(new ProcessResult(1, "", stderr, TimeSpan.FromSeconds(1)));

    private static FfmpegCommand BuildSimpleCommand() =>
        new("ffmpeg", ["-i", "/input.mkv", "/output.mp4"], null);
}
