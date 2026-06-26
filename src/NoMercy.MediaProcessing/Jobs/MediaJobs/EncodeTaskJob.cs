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
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Events;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Resources;
using NoMercy.Storage;
using NoMercyQueue;
using Serilog.Events;

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
public class EncodeTaskJob : AbstractEncoderJob, IHasResourceRequirement, IJobStorageInjector
{
    private IEncodingOrchestrator? _encodingOrchestrator;
    private IEncoderProcessRegistry? _encoderProcessRegistry;

    public void InjectStorageServices(IServiceProvider serviceProvider)
    {
        _encodingOrchestrator = serviceProvider.GetRequiredService<IEncodingOrchestrator>();
        _encoderProcessRegistry = serviceProvider.GetRequiredService<IEncoderProcessRegistry>();
    }
    public override string QueueName =>
        Task.Resources?.GpuDeviceKey is not null ? "encoder-gpu" : "encoder-cpu";

    public override int Priority => 4;

    /// <summary>Preset that defines the full encoding profile for this job.</summary>
    public Ulid PresetId { get; set; }

    /// <summary>Task descriptor from the coordinator's decompose call.</summary>
    public DecomposedTask Task { get; set; } = null!;

    /// <inheritdoc/>
    public ResourceRequirement? ResourceRequirement => Task.Resources;

    /// <summary>
    /// Destination output directory for this encode task. Set at dispatch time
    /// so orphan-recovery can locate the crash checkpoint without a DB round-trip.
    /// </summary>
    public string? OutputDirectory { get; set; }

    public override async Task Handle()
    {
        await using MediaContext context = new();
        await using LibraryRepository libraryRepository = new(context, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        EncodingProfile encodingProfile;
        try
        {
            encodingProfile = PresetResolver.Resolve(PresetId, new DbPresetLookup(context));
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"[EncodeTaskJob] Skipping task '{Task.Label}' for preset {PresetId}: resolve failed — {ex.Message}",
                LogEventLevel.Warning
            );
            await PublishCompletedAsync(success: false, error: ex.Message, artifacts: []);
            return;
        }

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
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

        IStorage destinationStorage = StorageFactory.For(folder.Id, folder.DriverId, folder.Path);

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(SourceDriverId.Value, SourceDriverId.Value, string.Empty)
            : destinationStorage;

        EncodingRequest request = new(
            InputPath: InputFile,
            OutputDirectory: fileMetadata.Path,
            Profile: encodingProfile,
            MediaTitle: fileMetadata.FileName,
            SourceStorage: sourceStorage,
            DestinationStorage: destinationStorage
        );

        // Propagate StatsFilePath from the task descriptor so TwoPassStrategyBase
        // receives the coordinator-resolved path for Pass2 tasks.
        if (!string.IsNullOrEmpty(Task.StatsFilePath))
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
            EncodingResult result = await orchestrator.EncodeAsync(request, Task, progressObserver);
            stopwatch.Stop();

            if (!result.Success)
            {
                string errorMsg =
                    result.Error?.Message ?? result.EnrichedError?.Message ?? "encode failed";
                Logger.Encoder(
                    $"[EncodeTaskJob] Task '{Task.Label}' failed: {errorMsg}",
                    LogEventLevel.Warning
                );
                await PublishCompletedAsync(success: false, error: errorMsg, artifacts: []);
                return;
            }

            Logger.Encoder(
                $"[EncodeTaskJob] Task '{Task.Label}' completed in {stopwatch.Elapsed.TotalSeconds:F1}s"
            );

            List<string> artifactPaths = result
                .Artifacts.Select(artifact => artifact.Path)
                .ToList();
            await PublishCompletedAsync(success: true, error: null, artifacts: artifactPaths);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Encoder(
                $"[EncodeTaskJob] Task '{Task.Label}' threw: {ex.Message}",
                LogEventLevel.Error
            );
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
        await WriteOutcomeRowAsync(success, error, artifacts);

        if (!EventBusProvider.IsConfigured)
            return;

        await EventBusProvider.Current.PublishAsync(
            new EncodeTaskCompletedEvent
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
            await WriteBundleOutcomesAsync(bundledIds, success, error, artifacts);
            return;
        }

        try
        {
            await using MediaContext outcomeContext = new();
            bool alreadyExists = await outcomeContext.EncodeTaskOutcomes.AnyAsync(row =>
                row.TaskId == Task.TaskId
            );

            if (alreadyExists)
                return;

            string? artifactsJson = artifacts.Count > 0 ? string.Join("\n", artifacts) : null;

            outcomeContext.EncodeTaskOutcomes.Add(
                new EncodeTaskOutcome
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
            Logger.Encoder(
                $"[EncodeTaskJob] Failed to write outcome row for task '{Task.TaskId}': {ex.Message}",
                LogEventLevel.Warning
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
                    .EncodeTaskOutcomes.Where(row => bundledIds.Contains(row.TaskId))
                    .Select(row => row.TaskId)
                    .ToListAsync()
            ).ToHashSet();

            string? artifactsJson = artifacts.Count > 0 ? string.Join("\n", artifacts) : null;

            foreach (string id in bundledIds)
            {
                if (existing.Contains(id))
                    continue;

                outcomeContext.EncodeTaskOutcomes.Add(
                    new EncodeTaskOutcome
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
            Logger.Encoder(
                $"[EncodeTaskJob] Failed to write bundle outcome rows for {bundledIds.Length} tasks: {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    private async Task<FileMetadata> GetFileMetaData(Folder folder, MediaContext context)
    {
        Movie? movie = folder.FolderLibraries.Any(x => x.Library.Type == MediaTypes.MovieMediaType)
            ? await context.Movies.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == Id.ToInt())
            : null;

        Episode? episode = folder.FolderLibraries.Any(x =>
            x.Library.Type == MediaTypes.TvMediaType || x.Library.Type == MediaTypes.AnimeMediaType
        )
            ? await context.Episodes.Include(x => x.Tv).FirstOrDefaultAsync(x => x.Id == Id.ToInt())
            : null;

        if (movie is null && episode is null)
            return new() { Success = false };

        string folderName =
            movie?.CreateFolderName().Replace("/", "")
            ?? episode!.Tv.CreateFolderName().Replace("/", "") + episode.CreateFolderName();

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
    }
}
