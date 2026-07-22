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
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Shared;
using NoMercy.Storage;

namespace NoMercy.Encoder.Strategies;

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

    /// <summary>
    /// Two-pass decomposition: one Pass1 task per video variant (each
    /// analyzing its rung independently), followed by one Pass2 task per
    /// video variant. The coordinator must run all Pass1 tasks to completion
    /// and propagate the stats file path before enqueuing Pass2 children.
    ///
    /// Audio, subtitle, and thumbnail tasks run in parallel with Pass2.
    /// </summary>
    public DecomposedTask[] Decompose(OutputPlan plan, string groupTag)
    {
        List<DecomposedTask> tasks = [];

        for (int i = 0; i < plan.VideoOutputs.Length; i++)
        {
            VideoOutputPlan video = plan.VideoOutputs[i];
            tasks.Add(
                item: new(
                    TaskId: $"{groupTag}-pass1-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Pass1,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.ForVideoOutput(video: video),
                    EstimatedCostUnits: EstimateVideoCost(video: video),
                    Label: $"pass1 {video.Width}p {video.EncoderName}"
                )
            );
        }

        for (int i = 0; i < plan.VideoOutputs.Length; i++)
        {
            VideoOutputPlan video = plan.VideoOutputs[i];
            tasks.Add(
                item: new(
                    TaskId: $"{groupTag}-pass2-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Pass2,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.ForVideoOutput(video: video),
                    EstimatedCostUnits: EstimateVideoCost(video: video),
                    StatsFilePath: null,
                    Label: $"pass2 {video.Width}p {video.EncoderName}"
                )
            );
        }

        for (int i = 0; i < plan.AudioOutputs.Length; i++)
        {
            AudioOutputPlan audio = plan.AudioOutputs[i];
            tasks.Add(
                item: new(
                    TaskId: $"{groupTag}-audio-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Audio,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(cpuThreads: 1),
                    EstimatedCostUnits: 1,
                    Label: $"{audio.Language ?? "und"} {audio.EncoderName}"
                )
            );
        }

        for (int i = 0; i < plan.SubtitleOutputs.Length; i++)
        {
            SubtitleOutputPlan sub = plan.SubtitleOutputs[i];
            tasks.Add(
                item: new(
                    TaskId: $"{groupTag}-sub-{i}",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Subtitle,
                    OutputIndex: i,
                    Resources: TaskResourceHelper.CpuOnly(cpuThreads: 1),
                    EstimatedCostUnits: 1,
                    Label: $"sub {sub.Language ?? "und"}"
                )
            );
        }

        if (plan.Thumbnails is not null)
        {
            tasks.Add(
                item: new(
                    TaskId: $"{groupTag}-thumbs",
                    ParentJobId: 0,
                    GroupTag: groupTag,
                    Kind: EncodeTaskKind.Thumbnails,
                    OutputIndex: 0,
                    Resources: TaskResourceHelper.CpuOnly(cpuThreads: 1),
                    EstimatedCostUnits: 1,
                    Label: "thumbnails"
                )
            );
        }

        if (plan is { GenerateChapterThumbs: true, Chapters.Count: > 0 })
        {
            int count = plan.Chapters.Count;
            for (int i = 0; i < count; i++)
            {
                ChapterInfo chapter = plan.Chapters[index: i];
                tasks.Add(
                    item: new(
                        TaskId: $"{groupTag}-chapter-{i}",
                        ParentJobId: 0,
                        GroupTag: groupTag,
                        Kind: EncodeTaskKind.Chapters,
                        OutputIndex: i,
                        Resources: TaskResourceHelper.CpuOnly(cpuThreads: 1),
                        EstimatedCostUnits: 1,
                        Label: $"chapter still {i + 1}/{count} @ {chapter.Start.TotalSeconds:F0}s"
                    )
                );
            }
        }

        if (tasks.Count == 0)
            return [IEncodingStrategy.WholeTask(groupTag: groupTag)];

        return tasks.ToArray();
    }

    private static int EstimateVideoCost(VideoOutputPlan video)
    {
        if (video.Width >= 3840)
            return 8;
        if (video.Width >= 1920)
            return 4;
        if (video.Width >= 1280)
            return 2;
        return 1;
    }

    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        // Use per-folder destination storage from the request when available;
        // fall back to the DI-injected singleton for default installs.
        IStorage effectiveStorage = request.DestinationStorage ?? request.SourceStorage ?? storage;

        // When the coordinator decomposes a two-pass encode into child tasks it
        // sets Options.Pass explicitly so each child runs exactly one pass.
        // Only the legacy inline path (Options.Pass == null) runs both passes.
        if (request.Options?.Pass == EncodingPass.One)
            return await RunSinglePassOneAsync(request: request, effectiveStorage: effectiveStorage, progress: progress, ct: ct);

        if (request.Options?.Pass == EncodingPass.Two)
            return await RunSinglePassTwoAsync(request: request, effectiveStorage: effectiveStorage, progress: progress, ct: ct);

        try
        {
            EncodingResult result = await EncodeInternalAsync(
                request: request,
                stor: effectiveStorage,
                progress: progress,
                ct: ct
            );

            if (!result.Success)
            {
                // Non-cancel failure (ffmpeg exited non-zero). Sweep any partial
                // destination output so the directory is not left in a half-written
                // state. The crash checkpoint written by ExecuteStage is intentionally
                // left intact so the orphan-recovery path can re-queue with resume.
                DeletePartialOutput(
                    outputDirectory: request.OutputDirectory,
                    stor: effectiveStorage,
                    preserveCheckpoint: true
                );
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // User cancelled — delete the checkpoint so there is no stale
            // resume point. Also remove any partial output the encode wrote.
            // Both operations are best-effort: log and continue on error.
            await DeleteCheckpointOnCancelAsync(outputDirectory: request.OutputDirectory);
            DeletePartialOutput(
                outputDirectory: request.OutputDirectory,
                stor: effectiveStorage,
                preserveCheckpoint: false
            );
            throw;
        }
    }

    /// <summary>
    /// Runs only the first pass of the two-pass encode for a single variant.
    /// Called by child <c>EncodeTaskJob</c> instances for Pass1 tasks.
    /// Does NOT write a checkpoint — the coordinator tracks phase state durably.
    /// </summary>
    private async Task<EncodingResult> RunSinglePassOneAsync(
        EncodingRequest request,
        IStorage effectiveStorage,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string statsFilePath =
            request.Options?.StatsFilePath ?? ResolveStatsFilePath(request: request, stor: effectiveStorage);

        int variantIndex = request.Options?.Pass1VariantIndex ?? 0;
        string stageName = $"Pass 1 variant {variantIndex}";

        progress?.OnStageStarted(stageName: stageName);

        EncodingRequest pass1Request = request with
        {
            Options = (request.Options ?? new EncodingOptions()) with
            {
                Pass = EncodingPass.One,
                StatsFilePath = statsFilePath,
                Pass1VariantIndex = variantIndex,
            },
        };

        EncodingResult result = await encoder.EncodeAsync(request: pass1Request, progress: progress, ct: ct);

        if (result.Success)
            progress?.OnStageCompleted(stageName: stageName, duration: result.Duration);

        return result;
    }

    /// <summary>
    /// Runs only the second pass of the two-pass encode.
    /// Called by child <c>EncodeTaskJob</c> instances for Pass2 tasks.
    /// Requires <c>request.Options.StatsFilePath</c> to be set by the coordinator.
    /// </summary>
    private async Task<EncodingResult> RunSinglePassTwoAsync(
        EncodingRequest request,
        IStorage effectiveStorage,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string statsFilePath =
            request.Options?.StatsFilePath ?? ResolveStatsFilePath(request: request, stor: effectiveStorage);

        progress?.OnStageStarted(stageName: "Pass 2");

        EncodingRequest pass2Request = request with
        {
            Options = (request.Options ?? new EncodingOptions()) with
            {
                Pass = EncodingPass.Two,
                StatsFilePath = statsFilePath,
            },
        };

        EncodingResult result = await encoder.EncodeAsync(request: pass2Request, progress: progress, ct: ct);

        if (result.Success)
        {
            progress?.OnStageCompleted(stageName: "Pass 2", duration: result.Duration);
            DeleteStatsFiles(statsFilePath: statsFilePath, stor: effectiveStorage);
        }

        return result;
    }

    private async Task DeleteCheckpointOnCancelAsync(string outputDirectory)
    {
        try
        {
            await checkpointStore.DeleteAsync(outputDirectory: outputDirectory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to delete checkpoint after cancellation for {OutputDirectory}",
                args: outputDirectory
            );
        }
    }

    private void DeletePartialOutput(string outputDirectory, IStorage stor, bool preserveCheckpoint)
    {
        try
        {
            if (!stor.Exists(path: outputDirectory))
                return;

            foreach (
                StorageEntry entry in stor.List(path: outputDirectory, pattern: "*", recursive: true)
                    .Where(predicate: e => !e.IsDirectory)
                    .Where(predicate: e =>
                        !preserveCheckpoint
                        || !Path.GetFileName(path: e.Path)
                            .Equals(
                                value: CheckpointFileNames.FileName,
                                comparisonType: StringComparison.OrdinalIgnoreCase
                            )
                    )
            )
            {
                try
                {
                    stor.Delete(path: entry.Path);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        exception: ex,
                        message: "Failed to delete partial output file {File} after cancellation",
                        args: entry.Path
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                exception: ex,
                message: "Failed to enumerate partial output for deletion after cancellation in {OutputDirectory}",
                args: outputDirectory
            );
        }
    }

    private async Task<EncodingResult> EncodeInternalAsync(
        EncodingRequest request,
        IStorage stor,
        IProgressObserver? progress,
        CancellationToken ct
    )
    {
        string statsFilePath = ResolveStatsFilePath(request: request, stor: stor);
        VideoOutput[] profileVideoOutputs = PlanStageHelpers.EnumerateVideo(profile: request.Profile);
        int variantCount = Math.Max(val1: 1, val2: profileVideoOutputs.Length);

        JobCheckpoint? checkpoint = await checkpointStore.LoadAsync(outputDirectory: request.OutputDirectory, ct: ct);
        bool pass1AlreadyDone =
            checkpoint is { Pass1Completed: true }
            && !string.IsNullOrEmpty(value: checkpoint.StatsFilePath)
            && AllVariantStatsPresent(basePath: checkpoint.StatsFilePath!, variantCount: variantCount, stor: stor);

        if (pass1AlreadyDone)
        {
            statsFilePath = checkpoint!.StatsFilePath!;
            progress?.OnStageStarted(stageName: "Pass 1 (resumed from checkpoint)");
            progress?.OnStageCompleted(stageName: "Pass 1 (resumed from checkpoint)", duration: TimeSpan.Zero);
            logger.LogInformation(
                message: "Resuming 2-pass encode for {Input} — pass 1 already done at {Stats} ({Variants} variants)", args: [request.InputPath, statsFilePath, variantCount]
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
                progress?.OnStageStarted(stageName: stageName);

                EncodingRequest pass1Request = request with
                {
                    Options = (request.Options ?? new EncodingOptions()) with
                    {
                        Pass = EncodingPass.One,
                        StatsFilePath = statsFilePath,
                        Pass1VariantIndex = variantIndex,
                    },
                };

                EncodingResult pass1Result = await encoder.EncodeAsync(request: pass1Request, progress: progress, ct: ct);
                if (!pass1Result.Success)
                {
                    logger.LogWarning(
                        message: "Pass 1 variant {Index} failed for {Input}: {Message}", args: [variantIndex, request.InputPath, pass1Result.Error?.Message]
                    );
                    return pass1Result;
                }

                progress?.OnStageCompleted(stageName: stageName, duration: pass1Result.Duration);
            }

            await SaveCheckpointAsync(request: request, statsFilePath: statsFilePath, ct: ct);
        }

        progress?.OnStageStarted(stageName: "Pass 2");
        EncodingRequest pass2Request = request with
        {
            Options = (request.Options ?? new EncodingOptions()) with
            {
                Pass = EncodingPass.Two,
                StatsFilePath = statsFilePath,
            },
        };

        EncodingResult pass2Result = await encoder.EncodeAsync(request: pass2Request, progress: progress, ct: ct);
        if (!pass2Result.Success)
        {
            logger.LogWarning(
                message: "Pass 2 failed for {Input}: {Message}", args: [request.InputPath, pass2Result.Error?.Message]
            );
            return pass2Result;
        }

        progress?.OnStageCompleted(stageName: "Pass 2", duration: pass2Result.Duration);

        await checkpointStore.DeleteAsync(outputDirectory: request.OutputDirectory, ct: ct);
        DeleteStatsFiles(statsFilePath: statsFilePath, stor: stor);

        return pass2Result;
    }

    private static string ResolveStatsFilePath(EncodingRequest request, IStorage stor)
    {
        string statsDir = Path.Combine(path1: request.OutputDirectory, path2: ".2pass");
        stor.CreateDirectory(path: statsDir);
        return Path.Combine(path1: statsDir, path2: "x264");
    }

    private async Task SaveCheckpointAsync(
        EncodingRequest request,
        string statsFilePath,
        CancellationToken ct
    )
    {
        JobCheckpoint checkpoint = new(
            JobId: $"{Path.GetFileNameWithoutExtension(path: request.InputPath)}-2pass-{Format}",
            InputPath: request.InputPath,
            OutputDirectory: request.OutputDirectory,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFilePath,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );
        await checkpointStore.SaveAsync(checkpoint: checkpoint, ct: ct);
    }

    private void DeleteStatsFiles(string statsFilePath, IStorage stor)
    {
        string? dir = Path.GetDirectoryName(path: statsFilePath);
        if (dir is null || !stor.Exists(path: dir))
            return;

        string baseName = Path.GetFileName(path: statsFilePath);
        // Covers every variant's output — x264 writes {base}_v{i}-0.log and
        // {base}_v{i}-0.log.mbtree. `{baseName}_v*` matches all of them.
        foreach (
            string file in stor.List(path: dir, pattern: $"{baseName}_v*", recursive: false)
                .Where(predicate: e => !e.IsDirectory)
                .Select(selector: e => e.Path)
        )
        {
            try
            {
                stor.Delete(path: file);
            }
            catch (Exception)
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
    private static bool AllVariantStatsPresent(string basePath, int variantCount, IStorage stor)
    {
        for (int i = 0; i < variantCount; i++)
        {
            string variantBase = $"{basePath}_v{i}";
            // x264 writes {variantBase}-0.log — that's the signal we look for.
            if (!stor.Exists(path: $"{variantBase}-0.log"))
                return false;
        }
        return true;
    }
}
