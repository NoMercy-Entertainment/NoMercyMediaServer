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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline.Stages;

public record ExecuteInput(
    FfmpegCommand[] Commands,
    TimeSpan InputDuration,
    IProgressObserver? Progress = null
);

public class ExecuteStage(
    IFfmpegExecutor executor,
    ICheckpointStore checkpointStore,
    ILogger<ExecuteStage> logger
) : IPipelineStage<ExecuteInput, ExecutionResult[]>, IExecutionStage
{
    public string Name => "Execute";

    public async Task<StageResult> ExecuteAsync(
        ExecuteInput input,
        EncodingContext context,
        CancellationToken ct
    )
    {
        logger.LogInformation(
            message: "[{CorrelationId}] Executing {Count} command(s)", args: [context.CorrelationId, input.Commands.Length]
        );

        List<ExecutionResult> results = [];
        long lastProgressMs = 0;

        try
        {
            for (int i = 0; i < input.Commands.Length; i++)
            {
                FfmpegCommand cmd = input.Commands[i];

                // Only the main encode command (index 0) reports progress.
                // Post-processing commands (subtitles, fonts) are short-lived.
                Action<EncodingProgress>? onProgress = null;
                if (i == 0 && input.Progress is not null)
                {
                    onProgress = progress =>
                    {
                        lastProgressMs = (long)(progress.CurrentTimeSeconds * 1000);
                        input.Progress.OnProgress(progress: progress);
                    };
                }
                else if (i == 0)
                {
                    onProgress = progress =>
                    {
                        lastProgressMs = (long)(progress.CurrentTimeSeconds * 1000);
                    };
                }

                ExecutionResult result = await executor.ExecuteAsync(
                    command: cmd,
                    inputDuration: input.InputDuration,
                    onProgress: onProgress,
                    correlationId: context.CorrelationId,
                    ct: ct
                );

                results.Add(item: result);

                if (!result.Success)
                {
                    // Command 0 is the main encode — must succeed.
                    // Subsequent commands (subtitle extraction, font extraction, thumbnails)
                    // are post-processing — failures are logged but not fatal.
                    if (i == 0)
                    {
                        EncodingError error =
                            result.Error
                            ?? new EncodingError(
                                Kind: EncodingErrorKind.ProcessCrashed,
                                Message: "FFmpeg exited with non-zero code",
                                FfmpegStderr: result.StdErr,
                                StageName: Name,
                                Recoverable: true
                            );

                        await WriteCrashCheckpointAsync(context: context, lastProgressMs: lastProgressMs, stderrTail: result.StdErr, ct: ct);

                        return new StageFailure(Error: error);
                    }

                    logger.LogWarning(
                        message: "[{CorrelationId}] Post-process command {Index} failed (non-fatal): exit={ExitCode}", args: [context.CorrelationId, i, result.ExitCode]
                    );
                }
            }

            return new StageSuccess<ExecutionResult[]>(Value: results.ToArray());
        }
        finally
        {
            CleanupDrmKeyArtifacts(commands: input.Commands, correlationId: context.CorrelationId);
        }
    }

    /// <summary>
    /// Aes128HlsDrmProcessor writes drm.key/drm_keyinfo.txt to a per-encode
    /// directory under StoragePaths.TempRoot (never OutputDirectory — that
    /// directory is published to the served destination). Ffmpeg has now
    /// either consumed that -hls_key_info_file or failed trying, so the temp
    /// directory can go. Only ever deletes paths under TempRoot — a defensive
    /// scope check in case a future DRM processor writes artifacts elsewhere.
    /// </summary>
    private void CleanupDrmKeyArtifacts(FfmpegCommand[] commands, string correlationId)
    {
        foreach (FfmpegCommand cmd in commands)
        {
            int idx = Array.IndexOf(array: cmd.Arguments, value: "-hls_key_info_file");
            if (idx < 0 || idx + 1 >= cmd.Arguments.Length)
                continue;

            string keyInfoPath = cmd.Arguments[idx + 1];
            string? tempDir = Path.GetDirectoryName(path: keyInfoPath);
            if (string.IsNullOrEmpty(value: tempDir))
                continue;

            string fullTempDir = Path.GetFullPath(path: tempDir);
            string fullTempRoot = Path.GetFullPath(path: StoragePaths.TempRoot);
            if (!fullTempDir.StartsWith(value: fullTempRoot, comparisonType: StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (Directory.Exists(path: fullTempDir))
                    Directory.Delete(path: fullTempDir, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    exception: ex,
                    message: "[{CorrelationId}] Failed to delete DRM key temp directory {Directory}", args: [correlationId, fullTempDir]
                );
            }
        }
    }

    private async Task WriteCrashCheckpointAsync(
        EncodingContext context,
        long lastProgressMs,
        string stderrTail,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(value: context.OutputDirectory))
            return;

        try
        {
            JobCheckpoint checkpoint = new(
                JobId: context.CorrelationId,
                InputPath: context.InputPath ?? string.Empty,
                OutputDirectory: context.OutputDirectory,
                CompletedGroupIndices: [],
                LastUpdated: DateTime.UtcNow,
                LastProgressMs: lastProgressMs,
                LastFfmpegStderrTail: TailStderr(stderr: stderrTail),
                FailedAt: DateTime.UtcNow
            );

            await checkpointStore.SaveAsync(checkpoint: checkpoint, ct: ct);

            logger.LogWarning(
                message: "[{CorrelationId}] Crash checkpoint saved at {OutputDirectory} — LastProgressMs={Ms}", args: [context.CorrelationId, context.OutputDirectory, lastProgressMs]
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "[{CorrelationId}] Failed to save crash checkpoint",
                args: context.CorrelationId
            );
        }
    }

    private static string TailStderr(string stderr)
    {
        const int maxBytes = 16 * 1024;
        if (string.IsNullOrEmpty(value: stderr) || stderr.Length <= maxBytes)
            return stderr;

        return stderr[^maxBytes..];
    }
}
