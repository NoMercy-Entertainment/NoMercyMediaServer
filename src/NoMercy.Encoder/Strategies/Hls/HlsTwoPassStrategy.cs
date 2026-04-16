namespace NoMercy.Encoder.Strategies.Hls;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;

/// <summary>
/// HLS 2-pass strategy. Runs a quick video-only analysis pass that writes
/// encoder statistics to a temp file, then a second pass that reads those
/// stats and produces the full HLS output at a much more accurate target
/// bitrate than single-pass can achieve.
///
/// Compared to single-pass at the same target bitrate, 2-pass produces
/// noticeably cleaner output on complex scenes (dark, high-motion, grain)
/// because the encoder spends its bit budget where it's needed most.
///
/// Only valid for software encoders — NVENC / QSV / AMF ignore the stats
/// file in pass 2 and give no quality benefit. The strategy still runs
/// both passes if a hardware encoder is resolved (to keep behavior simple);
/// users configuring 2-pass on hardware just waste pass-1 time. The
/// validation lives at profile-configuration time, not in the strategy.
///
/// On crash / cancel: pass 1 writes a checkpoint (Pass1Completed = true,
/// StatsFilePath set). On resume, if the checkpoint is present and the
/// stats file still exists on disk, pass 1 is skipped. Otherwise both
/// passes re-run from scratch.
/// </summary>
public class HlsTwoPassStrategy(
    IEncoder encoder,
    ICheckpointStore checkpointStore,
    ILogger<HlsTwoPassStrategy> logger
) : IEncodingStrategy
{
    public OutputFormat Format => OutputFormat.Hls;
    public EncodeMode EncodeMode => EncodeMode.TwoPass;

    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string statsFilePath = ResolveStatsFilePath(request);
        JobCheckpoint? checkpoint = await checkpointStore.LoadAsync(request.OutputDirectory, ct);
        bool pass1AlreadyDone =
            checkpoint is { Pass1Completed: true }
            && !string.IsNullOrEmpty(checkpoint.StatsFilePath)
            && File.Exists(checkpoint.StatsFilePath);

        // Pass 1 — skip if the checkpoint says it's done and the stats file
        // still exists on disk.
        if (pass1AlreadyDone)
        {
            // Respect the stats file location recorded in the checkpoint,
            // not whatever we'd resolve fresh.
            statsFilePath = checkpoint!.StatsFilePath!;
            progress?.OnStageStarted("Pass 1 (resumed from checkpoint)");
            progress?.OnStageCompleted("Pass 1 (resumed from checkpoint)", TimeSpan.Zero);
            logger.LogInformation(
                "Resuming 2-pass encode for {Input} — pass 1 already done at {Stats}",
                request.InputPath,
                statsFilePath
            );
        }
        else
        {
            progress?.OnStageStarted("Pass 1");
            EncodingRequest pass1Request = request with
            {
                Options = (request.Options ?? new EncodingOptions()) with
                {
                    Pass = EncodingPass.One,
                    StatsFilePath = statsFilePath,
                },
            };

            EncodingResult pass1Result = await encoder.EncodeAsync(pass1Request, progress, ct);
            if (!pass1Result.Success)
            {
                logger.LogWarning(
                    "Pass 1 failed for {Input}: {Message}",
                    request.InputPath,
                    pass1Result.Error?.Message
                );
                return pass1Result;
            }

            progress?.OnStageCompleted("Pass 1", pass1Result.Duration);
            await SaveCheckpointAsync(request, statsFilePath, ct);
        }

        // Pass 2 — the final HLS encode.
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

        // Success: drop the checkpoint and clean up stats files.
        await checkpointStore.DeleteAsync(request.OutputDirectory, ct);
        DeleteStatsFiles(statsFilePath);

        return pass2Result;
    }

    private static string ResolveStatsFilePath(EncodingRequest request)
    {
        // Stats lives alongside the output directory so a crash-then-resume
        // finds it without needing OS temp-path affinity. x264 writes
        // {base}-0.log and {base}-0.log.mbtree; the -passlogfile argument
        // is the base path (no extension).
        string statsDir = Path.Combine(request.OutputDirectory, ".2pass");
        Directory.CreateDirectory(statsDir);
        return Path.Combine(statsDir, "x264");
    }

    private async Task SaveCheckpointAsync(
        EncodingRequest request,
        string statsFilePath,
        CancellationToken ct
    )
    {
        JobCheckpoint checkpoint = new(
            JobId: $"{Path.GetFileNameWithoutExtension(request.InputPath)}-2pass",
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

    private static void DeleteStatsFiles(string statsFilePath)
    {
        // x264 produces {path}-0.log and {path}-0.log.mbtree; libvpx produces
        // {path}-0.log. Clean up anything matching so the .2pass dir is empty.
        string? dir = Path.GetDirectoryName(statsFilePath);
        if (dir is null || !Directory.Exists(dir))
            return;

        string baseName = Path.GetFileName(statsFilePath);
        foreach (
            string file in Directory.EnumerateFiles(
                dir,
                $"{baseName}-*",
                SearchOption.TopDirectoryOnly
            )
        )
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
