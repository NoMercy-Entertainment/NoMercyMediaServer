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

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Execution;

public class FfmpegExecutor(IProcessRunner processRunner, ILogger<FfmpegExecutor> logger)
    : IFfmpegExecutor
{
    private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(milliseconds: 500);
    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(seconds: 10);

    public async Task<ExecutionResult> ExecuteAsync(
        FfmpegCommand command,
        TimeSpan inputDuration,
        Action<EncodingProgress>? onProgress = null,
        string? correlationId = null,
        CancellationToken ct = default
    )
    {
        ProgressParser parser = new();
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTime lastProgressReport = DateTime.MinValue;

        // Metrics accumulators — sampled on every progress snapshot
        double speedSum = 0;
        double fpsSum = 0;
        double peakSpeed = 0;
        double peakFps = 0;
        long lastTotalSize = 0;
        int sampleCount = 0;

        // Kill signal: fires after a grace period once FFmpeg reports progress=end.
        // This prevents the process from hanging indefinitely after output is written.
        CancellationTokenSource killCts = new();
        bool hasProgressPipe = command.Arguments.Contains(value: "pipe:1");
        int ffmpegPid = 0;

        logger.LogDebug(
            message: "[{CorrelationId}] Executing: {Executable} {Args}", args: [correlationId, command.Executable, string.Join(separator: " ", value: command.Arguments)]
        );

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = parser.FeedLine(line: line);
            if (snapshot is null)
                return;

            // Collect metrics on every snapshot (not throttled)
            if (snapshot is { IsEnd: false, Speed: > 0 })
            {
                speedSum += snapshot.Speed;
                fpsSum += snapshot.Fps;
                peakSpeed = Math.Max(val1: peakSpeed, val2: snapshot.Speed);
                peakFps = Math.Max(val1: peakFps, val2: snapshot.Fps);
                lastTotalSize = snapshot.TotalSizeBytes;
                sampleCount++;
            }

            if (snapshot.IsEnd && hasProgressPipe)
            {
                lastTotalSize = snapshot.TotalSizeBytes;
                logger.LogDebug(
                    message: "[{CorrelationId}] progress=end received, starting {Grace}s exit grace period", args: [correlationId, ExitGracePeriod.TotalSeconds]
                );
                killCts.CancelAfter(delay: ExitGracePeriod);
            }

            if (onProgress is null)
                return;

            DateTime now = DateTime.UtcNow;
            bool throttled = now - lastProgressReport < ProgressThrottleInterval;

            if (!snapshot.IsEnd && throttled)
                return;

            lastProgressReport = now;

            double percent =
                inputDuration.TotalSeconds > 0
                    ? Math.Min(
                        val1: 100.0,
                        val2: snapshot.OutTime.TotalSeconds / inputDuration.TotalSeconds * 100.0
                    )
                    : 0;

            TimeSpan? remaining =
                snapshot.Speed > 0 && inputDuration.TotalSeconds > 0
                    ? TimeSpan.FromSeconds(
                        value: (inputDuration.TotalSeconds - snapshot.OutTime.TotalSeconds)
                               / snapshot.Speed
                    )
                    : null;

            // Parse the raw bitrate string from FFmpeg (e.g. "1234.5kbits/s"). This
            // string rides EncodingProgress.Bitrate straight into the dashboard's
            // SignalR payload (EventBusProgressObserver), so it must stay
            // period-decimal regardless of the host's locale.
            string bitrateStr = snapshot.BitrateKbps.HasValue
                ? $"{snapshot.BitrateKbps.Value.ToString(format: "F1", provider: CultureInfo.InvariantCulture)}kbits/s"
                : "N/A";

            EncodingProgress progress = new(
                CorrelationId: correlationId ?? "",
                PercentComplete: percent,
                Elapsed: stopwatch.Elapsed,
                EstimatedRemaining: remaining,
                CurrentFps: snapshot.Fps,
                CurrentSpeed: snapshot.Speed,
                CurrentStage: "Execute",
                CurrentOperation: null,
                BitrateKbps: snapshot.BitrateKbps.HasValue ? (int)snapshot.BitrateKbps.Value : null,
                Bitrate: bitrateStr,
                ProcessId: ffmpegPid,
                CurrentTimeSeconds: snapshot.OutTime.TotalSeconds,
                DurationSeconds: inputDuration.TotalSeconds
            );

            onProgress(obj: progress);
        }

        try
        {
            ProcessResult result = await processRunner.RunAsync(
                executable: command.Executable,
                arguments: command.Arguments,
                onStdOut: OnStdOut,
                onStdErr: null,
                workingDirectory: command.WorkingDirectory,
                cancellationToken: ct,
                killSignal: killCts.Token,
                onProcessStarted: pid => ffmpegPid = pid
            );

            stopwatch.Stop();

            if (result.IsSuccess)
            {
                ExecutionMetrics metrics = new(
                    AverageSpeed: sampleCount > 0 ? speedSum / sampleCount : 0,
                    AverageFps: sampleCount > 0 ? fpsSum / sampleCount : 0,
                    PeakSpeed: peakSpeed,
                    PeakFps: peakFps,
                    TotalSizeBytes: lastTotalSize
                );

                logger.LogInformation(
                    message: "[{CorrelationId}] FFmpeg completed in {Duration} (avg {Speed:F2}x, {Fps:F1} fps)", args: [correlationId, stopwatch.Elapsed, metrics.AverageSpeed, metrics.AverageFps]
                );

                return new(
                    Success: true,
                    ExitCode: 0,
                    StdErr: result.StdErr,
                    Duration: stopwatch.Elapsed,
                    Error: null,
                    Metrics: metrics
                );
            }

            EncodingError error = ClassifyError(stderr: result.StdErr, exitCode: result.ExitCode);
            logger.LogError(
                message: "[{CorrelationId}] FFmpeg failed: exit={ExitCode} error={ErrorKind}\nstderr: {StdErr}", args: [correlationId, result.ExitCode, error.Kind, result.StdErr]
            );

            return new(
                Success: false,
                ExitCode: result.ExitCode,
                StdErr: result.StdErr,
                Duration: stopwatch.Elapsed,
                Error: error
            );
        }
        finally
        {
            killCts.Dispose();
        }
    }

    private static EncodingError ClassifyError(string stderr, int exitCode)
    {
        string lower = stderr.ToLowerInvariant();

        EncodingErrorKind kind = lower switch
        {
            _ when lower.Contains(value: "no such file") => EncodingErrorKind.InputNotFound,
            _ when lower.Contains(value: "invalid data found") => EncodingErrorKind.InputCorrupt,
            _ when lower.Contains(value: "codec not currently supported") =>
                EncodingErrorKind.CodecUnavailable,
            _ when lower.Contains(value: "encoder") && lower.Contains(value: "not found") =>
                EncodingErrorKind.CodecUnavailable,
            _ when lower.Contains(value: "device") && lower.Contains(value: "cannot") =>
                EncodingErrorKind.HardwareFailure,
            _ when lower.Contains(value: "no space left") => EncodingErrorKind.DiskFull,
            _ when lower.Contains(value: "out of memory") => EncodingErrorKind.HardwareFailure,
            _ => EncodingErrorKind.ProcessCrashed,
        };

        return new(
            Kind: kind,
            Message: $"FFmpeg exited with code {exitCode}",
            FfmpegStderr: stderr.Length > 2000 ? stderr[^2000..] : stderr,
            StageName: "Execute",
            Recoverable: kind
                is EncodingErrorKind.HardwareFailure
                    or EncodingErrorKind.ProcessCrashed
        );
    }
}
