namespace NoMercy.Encoder.Execution;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Infrastructure;
using NoMercy.Encoder.Progress;

public class FfmpegExecutor(IProcessRunner processRunner, ILogger<FfmpegExecutor> logger)
    : IFfmpegExecutor
{
    private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(10);

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
        bool hasProgressPipe = command.Arguments.Contains("pipe:1");
        int ffmpegPid = 0;

        logger.LogDebug(
            "[{CorrelationId}] Executing: {Executable} {Args}",
            correlationId,
            command.Executable,
            string.Join(" ", command.Arguments)
        );

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = parser.FeedLine(line);
            if (snapshot is null)
                return;

            // Collect metrics on every snapshot (not throttled)
            if (!snapshot.IsEnd && snapshot.Speed > 0)
            {
                speedSum += snapshot.Speed;
                fpsSum += snapshot.Fps;
                peakSpeed = Math.Max(peakSpeed, snapshot.Speed);
                peakFps = Math.Max(peakFps, snapshot.Fps);
                lastTotalSize = snapshot.TotalSizeBytes;
                sampleCount++;
            }

            if (snapshot.IsEnd && hasProgressPipe)
            {
                lastTotalSize = snapshot.TotalSizeBytes;
                logger.LogDebug(
                    "[{CorrelationId}] progress=end received, starting {Grace}s exit grace period",
                    correlationId,
                    ExitGracePeriod.TotalSeconds
                );
                killCts.CancelAfter(ExitGracePeriod);
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
                        100.0,
                        snapshot.OutTime.TotalSeconds / inputDuration.TotalSeconds * 100.0
                    )
                    : 0;

            TimeSpan? remaining =
                snapshot.Speed > 0 && inputDuration.TotalSeconds > 0
                    ? TimeSpan.FromSeconds(
                        (inputDuration.TotalSeconds - snapshot.OutTime.TotalSeconds)
                            / snapshot.Speed
                    )
                    : null;

            // Parse the raw bitrate string from FFmpeg (e.g. "1234.5kbits/s")
            string bitrateStr = snapshot.BitrateKbps.HasValue
                ? $"{snapshot.BitrateKbps.Value:F1}kbits/s"
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

            onProgress(progress);
        }

        try
        {
            ProcessResult result = await processRunner.RunAsync(
                command.Executable,
                command.Arguments,
                OnStdOut,
                null,
                command.WorkingDirectory,
                ct,
                killCts.Token,
                pid => ffmpegPid = pid
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
                    "[{CorrelationId}] FFmpeg completed in {Duration} (avg {Speed:F2}x, {Fps:F1} fps)",
                    correlationId,
                    stopwatch.Elapsed,
                    metrics.AverageSpeed,
                    metrics.AverageFps
                );

                return new ExecutionResult(
                    Success: true,
                    ExitCode: 0,
                    StdErr: result.StdErr,
                    Duration: stopwatch.Elapsed,
                    Error: null,
                    Metrics: metrics
                );
            }

            EncodingError error = ClassifyError(result.StdErr, result.ExitCode);
            logger.LogError(
                "[{CorrelationId}] FFmpeg failed: exit={ExitCode} error={ErrorKind}\nstderr: {StdErr}",
                correlationId,
                result.ExitCode,
                error.Kind,
                result.StdErr
            );

            return new ExecutionResult(
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
            _ when lower.Contains("no such file") => EncodingErrorKind.InputNotFound,
            _ when lower.Contains("invalid data found") => EncodingErrorKind.InputCorrupt,
            _ when lower.Contains("codec not currently supported") =>
                EncodingErrorKind.CodecUnavailable,
            _ when lower.Contains("encoder") && lower.Contains("not found") =>
                EncodingErrorKind.CodecUnavailable,
            _ when lower.Contains("device") && lower.Contains("cannot") =>
                EncodingErrorKind.HardwareFailure,
            _ when lower.Contains("no space left") => EncodingErrorKind.DiskFull,
            _ when lower.Contains("out of memory") => EncodingErrorKind.HardwareFailure,
            _ => EncodingErrorKind.ProcessCrashed,
        };

        return new EncodingError(
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
