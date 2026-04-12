namespace NoMercy.Encoder.V3.Execution;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Infrastructure;
using NoMercy.Encoder.V3.Progress;

public class FfmpegExecutor(IProcessRunner processRunner, ILogger<FfmpegExecutor> logger)
    : IFfmpegExecutor
{
    private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(500);

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

        logger.LogDebug(
            "[{CorrelationId}] Executing: {Executable} {Args}",
            correlationId,
            command.Executable,
            string.Join(" ", command.Arguments)
        );

        void OnStdOut(string line)
        {
            FfmpegProgressSnapshot? snapshot = parser.FeedLine(line);
            if (snapshot is null || onProgress is null)
                return;

            DateTime now = DateTime.UtcNow;
            bool isEnd = snapshot.IsEnd;
            bool throttled = now - lastProgressReport < ProgressThrottleInterval;

            if (!isEnd && throttled)
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

            EncodingProgress progress = new(
                CorrelationId: correlationId ?? "",
                PercentComplete: percent,
                Elapsed: stopwatch.Elapsed,
                EstimatedRemaining: remaining,
                CurrentFps: snapshot.Fps,
                CurrentSpeed: snapshot.Speed,
                CurrentStage: "Execute",
                CurrentOperation: null
            );

            onProgress(progress);
        }

        ProcessResult result = await processRunner.RunAsync(
            command.Executable,
            command.Arguments,
            OnStdOut,
            null, // stderr not streamed — captured in result
            command.WorkingDirectory,
            ct
        );

        stopwatch.Stop();

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "[{CorrelationId}] FFmpeg completed in {Duration}",
                correlationId,
                stopwatch.Elapsed
            );

            return new ExecutionResult(
                Success: true,
                ExitCode: 0,
                StdErr: result.StdErr,
                Duration: stopwatch.Elapsed,
                Error: null
            );
        }

        EncodingError error = ClassifyError(result.StdErr, result.ExitCode);
        logger.LogError(
            "[{CorrelationId}] FFmpeg failed: exit={ExitCode} error={ErrorKind}",
            correlationId,
            result.ExitCode,
            error.Kind
        );

        return new ExecutionResult(
            Success: false,
            ExitCode: result.ExitCode,
            StdErr: result.StdErr,
            Duration: stopwatch.Elapsed,
            Error: error
        );
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
