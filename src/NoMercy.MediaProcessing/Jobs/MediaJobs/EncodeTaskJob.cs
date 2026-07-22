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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Events;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core;
using NoMercyQueue.Core.Interfaces;
using NoMercyQueue.Core.Resources;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Executes a single decomposed encode task produced by
/// <see cref="VideoEncodeJob"/> coordinator. One child job per video rung,
/// audio group, subtitle track, or thumbnail strip.
///
/// Routes to <c>encoder-gpu</c> when <see cref="DecomposedTask.Resources"/>
/// carries a non-null GPU device key; otherwise to <c>encoder-cpu</c>.
/// This keeps GPU-bound tasks (NVENC / AMF / QSV) from starving CPU-only
/// tasks and prevents the GPU session cap from being exceeded.
/// </summary>
[Serializable]
public class EncodeTaskJob
    : AbstractEncoderJob,
        IHasResourceRequirement,
        IJobStorageInjector,
        IResourceDegradable
{
    private IEncodingOrchestrator? _encodingOrchestrator;
    private IEncoderProcessRegistry? _encoderProcessRegistry;

    public new void InjectStorageServices(IServiceProvider serviceProvider)
    {
        base.InjectStorageServices(serviceProvider: serviceProvider);
        _encodingOrchestrator = serviceProvider.GetRequiredService<IEncodingOrchestrator>();
        _encoderProcessRegistry = serviceProvider.GetRequiredService<IEncoderProcessRegistry>();
    }

    public override string QueueName =>
        Task.Resources?.GpuDeviceKey is not null ? QueueNames.EncoderGpu : QueueNames.EncoderCpu;

    public override int Priority => 4;

    /// <summary>Preset that defines the full encoding profile for this job.</summary>
    public Ulid PresetId { get; set; }

    /// <summary>
    /// Set only for a smart-orchestrator merged run — every preset id unioned
    /// into the coordinated encode this task belongs to (index 0 is the
    /// primary preset). <see cref="Task"/>.OutputIndex is an index into the
    /// MERGED plan's output arrays, not any single preset's own plan, so this
    /// task must re-plan every listed preset and re-merge them the same way
    /// the coordinator did at dispatch time before it can make sense of that
    /// index. Null (the default) is every other run — <see cref="PresetId"/>
    /// alone resolves the single profile PlanStage re-derives a plan from,
    /// exactly as before.
    /// </summary>
    public Ulid[]? PresetIds { get; set; }

    /// <summary>Task descriptor from the coordinator's decompose call.</summary>
    public DecomposedTask Task { get; set; } = null!;

    /// <inheritdoc/>
    public ResourceRequirement? ResourceRequirement => Task.Resources;

    /// <summary>
    /// Re-plans this task to run without its GPU requirement — see
    /// <see cref="IResourceDegradable"/>. Called by the queue worker's budget
    /// gate when <see cref="Task"/>'s <see cref="ResourceRequirement.GpuDeviceKey"/>
    /// is not a registered device on this host (e.g. a preset pinned to
    /// <c>h264_amf</c> on a host whose only GPU is NVIDIA) — that requirement
    /// can never be granted, so it drops the GPU pin and reroutes to
    /// <c>encoder-cpu</c> via <see cref="QueueName"/> instead of looping at the
    /// budget gate forever.
    /// </summary>
    public IShouldQueue? DegradeToSoftware()
    {
        if (Task.Resources?.GpuDeviceKey is null)
            return null; // already CPU-only — nothing to degrade

        Task = Task with { Resources = Task.Resources with { GpuDeviceKey = null, GpuSlots = 0 } };

        return this;
    }

    /// <summary>
    /// Destination output directory for this encode task. Set at dispatch time
    /// so orphan-recovery can locate the crash checkpoint without a DB round-trip.
    /// </summary>
    public string? OutputDirectory { get; set; }

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using LibraryRepository libraryRepository = new(context: context, storageDriver: StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(folderId: FolderId);
        if (folder is null)
            return;

        EncodingProfile encodingProfile;
        try
        {
            encodingProfile = PresetResolver.Resolve(presetId: PresetId, lookup: new DbPresetLookup(context: context));
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                message: "[EncodeTaskJob] Skipping task '{Label}' for preset {PresetId}: resolve failed — {Message}", args: [Task.Label, PresetId, ex.Message]
            );
            await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
            return;
        }

        FileMetadata fileMetadata = await GetFileMetaData(folder: folder, context: context);
        if (!fileMetadata.Success)
        {
            await PublishCompletedAsync(
                success: false,
                error: "Could not resolve media metadata",
                artifacts: []
            );
            return;
        }

        IEncodingOrchestrator orchestrator = _encodingOrchestrator!;

        IStorage destinationStorage = StorageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(folderId: SourceDriverId.Value, driverId: SourceDriverId.Value, subPath: string.Empty)
            : destinationStorage;

        EncodingRequest request = new(
            InputPath: InputFile,
            OutputDirectory: fileMetadata.Path,
            Profile: encodingProfile,
            MediaTitle: fileMetadata.FileName,
            SourceStorage: sourceStorage,
            DestinationStorage: destinationStorage,
            // A bundled Whole task finalizes itself rather than deferring to the
            // coordinator, so it is the run that has to write manifest.json and
            // reconstruction.json — and it can only do that when it knows which
            // media it is encoding. Without this the plan carries no BundleLayout
            // and both files are silently skipped, leaving the output with no
            // record of how to reproduce or revert it.
            MediaItem: fileMetadata.MediaItem
        );

        // Smart-orchestrator merged run: Task.OutputIndex is an index into the
        // MERGED plan (union of every listed preset's video renditions), not
        // this job's own single-preset plan. Re-plan every preset in
        // PresetIds and re-merge them the same way the coordinator did at
        // dispatch time — PlanAsync is deterministic given the same profile
        // + source, so this reproduces the identical merged plan without
        // having to carry the whole thing through the queue payload.
        if (PresetIds is { Length: > 1 } presetIds)
        {
            List<EncodingRequest> mergeRequests = new(capacity: presetIds.Length);
            foreach (Ulid presetId in presetIds)
            {
                EncodingProfile presetProfile;
                try
                {
                    presetProfile = PresetResolver.Resolve(presetId: presetId, lookup: new DbPresetLookup(context: context));
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        message: "[EncodeTaskJob] Merged task '{Label}': preset {PresetId} resolve failed — {Message}", args: [Task.Label, presetId, ex.Message]
                    );
                    await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
                    return;
                }

                mergeRequests.Add(item: request with { Profile = presetProfile });
            }

            OutputPlan? mergedPlan = await orchestrator.PlanMergedAsync(requests: mergeRequests);
            if (mergedPlan is null)
            {
                string error =
                    $"Could not rebuild the merged plan for preset set [{string.Join(separator: ", ", values: presetIds)}]";
                Log.LogError(
                    message: "[EncodeTaskJob] Merged task '{Label}' failed: {Error}", args: [Task.Label, error]
                );
                await PublishCompletedAsync(success: false, error: error, artifacts: []);
                return;
            }

            request = request with
            {
                Options = (request.Options ?? new EncodingOptions()) with
                {
                    PrecomputedPlan = mergedPlan,
                },
            };
        }

        // Propagate StatsFilePath from the task descriptor so TwoPassStrategyBase
        // receives the coordinator-resolved path for Pass2 tasks.
        if (!string.IsNullOrEmpty(value: Task.StatsFilePath))
        {
            request = request with
            {
                Options = (request.Options ?? new EncodingOptions()) with
                {
                    StatsFilePath = Task.StatsFilePath,
                },
            };
        }

        IEncoderProcessRegistry? processRegistry = _encoderProcessRegistry;

        EventBusProgressObserver progressObserver = new(
            jobId: fileMetadata.Id,
            title: fileMetadata.Title,
            baseFolder: fileMetadata.Path,
            sharePath: fileMetadata.Path,
            registry: processRegistry
        );

        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request: request, task: Task, progress: progressObserver);
            stopwatch.Stop();

            if (!result.Success)
            {
                string errorMsg =
                    result.Error?.Message ?? result.EnrichedError?.Message ?? "encode failed";
                Log.LogWarning(
                    message: "[EncodeTaskJob] Task '{Label}' failed: {ErrorMsg}", args: [Task.Label, errorMsg]
                );
                await PublishCompletedAsync(success: false, error: errorMsg, artifacts: []);
                return;
            }

            Log.LogInformation(
                message: "[EncodeTaskJob] Task '{Label}' completed in {TotalSeconds:F1}s", args: [Task.Label, stopwatch.Elapsed.TotalSeconds]
            );

            List<string> artifactPaths = result
                .Artifacts.Select(selector: artifact => artifact.Path)
                .ToList();
            await PublishCompletedAsync(success: true, error: null, artifacts: artifactPaths);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.LogError(message: "[EncodeTaskJob] Task '{Label}' threw: {Message}", args: [Task.Label, ex.Message]);
            await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
            throw;
        }
    }

    /// <summary>
    /// Writes a durable <see cref="EncodeTaskOutcome"/> row to <c>media.db</c>
    /// BEFORE publishing the EventBus event. The outcome row is the authoritative
    /// source of truth that survives server restarts; the EventBus publish is
    /// best-effort for real-time dashboard updates only.
    /// </summary>
    private async Task PublishCompletedAsync(
        bool success,
        string? error,
        IReadOnlyList<string> artifacts
    )
    {
        await WriteOutcomeRowAsync(success: success, error: error, artifacts: artifacts);

        if (!EventBusProvider.IsConfigured)
            return;

        await EventBusProvider.Current.PublishAsync(
            @event: new EncodeTaskCompletedEvent
            {
                TaskId = Task.TaskId,
                ParentJobId = Task.ParentJobId,
                GroupTag = Task.GroupTag,
                Success = success,
                Error = error,
                Kind = Task.Kind,
                OutputArtifacts = artifacts,
            }
        );
    }

    private async Task WriteOutcomeRowAsync(
        bool success,
        string? error,
        IReadOnlyList<string> artifacts
    )
    {
        // Dispatch-time bundle: one ffmpeg covered N original stream-tasks.
        // Write one EncodeTaskOutcome row per bundled ID so the coordinator's
        // WaitChildren phase sees each stream completed. The synthetic Whole
        // wrapper task is not tracked by the coordinator and gets no row.
        string[]? bundledIds = Task.BundledTaskIds;
        if (bundledIds is { Length: > 0 })
        {
            await WriteBundleOutcomesAsync(bundledIds: bundledIds, success: success, error: error, artifacts: artifacts);
            return;
        }

        try
        {
            await using MediaContext outcomeContext = new();
            bool alreadyExists = await outcomeContext.EncodeTaskOutcomes.AnyAsync(predicate: row =>
                row.TaskId == Task.TaskId
            );

            if (alreadyExists)
                return;

            string? artifactsJson = artifacts.Count > 0 ? string.Join(separator: "\n", values: artifacts) : null;

            outcomeContext.EncodeTaskOutcomes.Add(
                entity: new()
                {
                    TaskId = Task.TaskId,
                    ParentJobId = Task.ParentJobId,
                    GroupTag = Task.GroupTag,
                    Success = success,
                    ErrorMessage = error,
                    Kind = Task.Kind.ToString(),
                    OutputArtifactsJson = artifactsJson,
                    CompletedAt = DateTime.UtcNow,
                }
            );

            await outcomeContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                message: "[EncodeTaskJob] Failed to write outcome row for task '{TaskId}': {Message}", args: [Task.TaskId, ex.Message]
            );
        }
    }

    private async Task WriteBundleOutcomesAsync(
        string[] bundledIds,
        bool success,
        string? error,
        IReadOnlyList<string> artifacts
    )
    {
        try
        {
            await using MediaContext outcomeContext = new();
            HashSet<string> existing = (
                await outcomeContext
                    .EncodeTaskOutcomes.Where(predicate: row => bundledIds.Contains(row.TaskId))
                    .Select(selector: row => row.TaskId)
                    .ToListAsync()
            ).ToHashSet();

            string? artifactsJson = artifacts.Count > 0 ? string.Join(separator: "\n", values: artifacts) : null;

            foreach (string id in bundledIds)
            {
                if (existing.Contains(item: id))
                    continue;

                outcomeContext.EncodeTaskOutcomes.Add(
                    entity: new()
                    {
                        TaskId = id,
                        ParentJobId = Task.ParentJobId,
                        GroupTag = Task.GroupTag,
                        Success = success,
                        ErrorMessage = error,
                        Kind = Task.Kind.ToString(),
                        OutputArtifactsJson = artifactsJson,
                        CompletedAt = DateTime.UtcNow,
                    }
                );
            }

            await outcomeContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                message: "[EncodeTaskJob] Failed to write bundle outcome rows for {Length} tasks: {Message}", args: [bundledIds.Length, ex.Message]
            );
        }
    }

    private async Task<FileMetadata> GetFileMetaData(Folder folder, MediaContext context)
    {
        Movie? movie = folder.FolderLibraries.Any(predicate: x => x.Library.Type == MediaTypes.MovieMediaType)
            ? await context.Movies.IgnoreQueryFilters().FirstOrDefaultAsync(predicate: x => x.Id == Id.ToInt())
            : null;

        Episode? episode = folder.FolderLibraries.Any(predicate: x =>
            x.Library.Type == MediaTypes.TvMediaType || x.Library.Type == MediaTypes.AnimeMediaType
        )
            ? await context.Episodes.Include(navigationPropertyPath: x => x.Tv).FirstOrDefaultAsync(predicate: x => x.Id == Id.ToInt())
            : null;

        if (movie is null && episode is null)
            return new() { Success = false };

        string folderName =
            movie?.CreateFolderName().Replace(oldValue: "/", newValue: "")
            ?? episode!.Tv.CreateFolderName().Replace(oldValue: "/", newValue: "") + episode.CreateFolderName();

        string title = movie?.CreateTitle() ?? episode!.CreateTitle();
        string fileName = movie?.CreateFileName() ?? episode!.CreateFileName();
        string basePath = folderName;
        int baseId = movie?.Id ?? episode!.Id;

        return new()
        {
            Success = true,
            FolderName = folderName,
            Title = title,
            FileName = fileName,
            Path = basePath,
            Id = baseId,
            MediaItem = MediaItemRefFactory.Create(movie: movie, episode: episode),
        };
    }

    private record FileMetadata
    {
        public bool Success { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Id { get; set; }

        /// <summary>
        /// Identifies the movie/episode being encoded. PlanStage needs it to
        /// resolve a BundleLayout, without which FinalizeStage writes neither
        /// manifest.json nor reconstruction.json. Pure identity: it never reaches
        /// the ffmpeg command, which is gated separately on
        /// EncodingOptions.EnableMetadataInjection.
        /// </summary>
        public MediaItemRef? MediaItem { get; set; }
    }
}
