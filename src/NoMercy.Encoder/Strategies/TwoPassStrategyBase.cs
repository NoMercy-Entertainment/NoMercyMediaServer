namespace NoMercy.Encoder.Strategies;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

/// <summary>
/// Shared 2-pass orchestration: pass 1 video-only analysis → checkpoint →
/// pass 2 full encode → checkpoint cleanup. Subclasses only need to declare
/// their <see cref="Format"/>. The actual command layout for each pass is
/// format-agnostic and lives in <c>BuildStage</c>.
///
/// Checkpoint resume: on start, loads any existing checkpoint; if
/// <c>Pass1Completed</c> is true and the stats file still exists on disk,
/// pass 1 is skipped. On pass 2 success the checkpoint is deleted and the
/// stats files are cleaned up.
///
/// Only makes sense for software encoders — hardware encoders (NVENC, QSV,
/// AMF) ignore the stats file and get no quality benefit from 2-pass. That
/// validation belongs at profile-configuration time; the strategy runs
/// whatever FFmpeg ends up resolving.
/// </summary>
public abstract class TwoPassStrategyBase(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger logger,
    IStorage storage
) : IEncodingStrategy
{
    public abstract OutputFormat Format { get; }
    public EncodeMode EncodeMode => EncodeMode.TwoPass;

    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string statsFilePath = ResolveStatsFilePath(request);
        int variantCount = Math.Max(1, request.Profile.VideoOutputs.Length);

        JobCheckpoint? checkpoint = await checkpointStore.LoadAsync(request.OutputDirectory, ct);
        bool pass1AlreadyDone =
            checkpoint is { Pass1Completed: true }
            && !string.IsNullOrEmpty(checkpoint.StatsFilePath)
            && AllVariantStatsPresent(checkpoint.StatsFilePath!, variantCount);

        if (pass1AlreadyDone)
        {
            statsFilePath = checkpoint!.StatsFilePath!;
            progress?.OnStageStarted("Pass 1 (resumed from checkpoint)");
            progress?.OnStageCompleted("Pass 1 (resumed from checkpoint)", TimeSpan.Zero);
            logger.LogInformation(
                "Resuming 2-pass encode for {Input} — pass 1 already done at {Stats} ({Variants} variants)",
                request.InputPath,
                statsFilePath,
                variantCount
            );
        }
        else
        {
            // Pass 1 runs once per variant, each with its own stats file.
            // BuildStage picks which variant to analyze based on Pass1VariantIndex
            // and appends _v{i} to the base stats path.
            for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
            {
                string stageName =
                    variantCount == 1
                        ? "Pass 1"
                        : $"Pass 1 variant {variantIndex + 1}/{variantCount}";
                progress?.OnStageStarted(stageName);

                EncodingRequest pass1Request = request with
                {
                    Options = (request.Options ?? new EncodingOptions()) with
                    {
                        Pass = EncodingPass.One,
                        StatsFilePath = statsFilePath,
                        Pass1VariantIndex = variantIndex,
                    },
                };

                EncodingResult pass1Result = await encoder.EncodeAsync(pass1Request, progress, ct);
                if (!pass1Result.Success)
                {
                    logger.LogWarning(
                        "Pass 1 variant {Index} failed for {Input}: {Message}",
                        variantIndex,
                        request.InputPath,
                        pass1Result.Error?.Message
                    );
                    return pass1Result;
                }

                progress?.OnStageCompleted(stageName, pass1Result.Duration);
            }

            await SaveCheckpointAsync(request, statsFilePath, ct);
        }

        progress?.OnStageStarted("Pass 2");
        EncodingRequest pass2Request = request with
        {
            Options = (request.Options ?? new EncodingOptions()) with
            {
                Pass = EncodingPass.Two,
                StatsFilePath = statsFilePath,
            },
        };

        EncodingResult pass2Result = await encoder.EncodeAsync(pass2Request, progress, ct);
        if (!pass2Result.Success)
        {
            logger.LogWarning(
                "Pass 2 failed for {Input}: {Message}",
                request.InputPath,
                pass2Result.Error?.Message
            );
            return pass2Result;
        }

        progress?.OnStageCompleted("Pass 2", pass2Result.Duration);

        await checkpointStore.DeleteAsync(request.OutputDirectory, ct);
        DeleteStatsFiles(statsFilePath, storage);

        return pass2Result;
    }

    private string ResolveStatsFilePath(EncodingRequest request)
    {
        string statsDir = Path.Combine(request.OutputDirectory, ".2pass");
        storage.CreateDirectory(statsDir);
        return Path.Combine(statsDir, "x264");
    }

    private async Task SaveCheckpointAsync(
        EncodingRequest request,
        string statsFilePath,
        CancellationToken ct
    )
    {
        JobCheckpoint checkpoint = new(
            JobId: $"{Path.GetFileNameWithoutExtension(request.InputPath)}-2pass-{Format}",
            InputPath: request.InputPath,
            OutputDirectory: request.OutputDirectory,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFilePath,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );
        await checkpointStore.SaveAsync(checkpoint, ct);
    }

    private void DeleteStatsFiles(string statsFilePath, IStorage stor)
    {
        string? dir = Path.GetDirectoryName(statsFilePath);
        if (dir is null || !stor.Exists(dir))
            return;

        string baseName = Path.GetFileName(statsFilePath);
        // Covers every variant's output — x264 writes {base}_v{i}-0.log and
        // {base}_v{i}-0.log.mbtree. `{baseName}_v*` matches all of them.
        foreach (
            string file in stor
                .List(dir, $"{baseName}_v*", recursive: false)
                .Where(e => !e.IsDirectory)
                .Select(e => e.Path)
        )
        {
            try
            {
                stor.Delete(file);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>
    /// Pass-1 resume check — verifies every variant's stats file exists.
    /// Missing any one forces the whole pass 1 to re-run for consistency;
    /// mixing fresh and stale stats across variants gives unreliable quality.
    /// </summary>
    private bool AllVariantStatsPresent(string basePath, int variantCount)
    {
        for (int i = 0; i < variantCount; i++)
        {
            string variantBase = $"{basePath}_v{i}";
            // x264 writes {variantBase}-0.log — that's the signal we look for.
            if (!storage.Exists($"{variantBase}-0.log"))
                return false;
        }
        return true;
    }
}
