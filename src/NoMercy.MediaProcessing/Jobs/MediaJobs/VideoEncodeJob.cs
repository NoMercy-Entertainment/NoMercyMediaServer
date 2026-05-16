using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using QueueJobDispatcher = NoMercyQueue.JobDispatcher;

namespace NoMercy.MediaProcessing.Jobs.MediaJobs;

/// <summary>
/// Coordinator job for video encoding. Implements a durable polling state machine
/// so that server restarts between child completions do not orphan the encode run.
///
/// <para><b>Phase flow for decomposable strategies (HLS, DASH):</b>
/// <c>Initial</c> → decomposes, dispatches all children, saves state, re-enqueues self →
/// <c>WaitChildren</c> → polls <c>EncodeTaskOutcomes</c> table until all TaskIds are present →
/// <c>Finalize</c> → opens fresh <see cref="MediaContext"/>, runs post-encode, completes.</para>
///
/// <para><b>Phase flow for two-pass strategies:</b>
/// <c>Initial</c> → dispatches Pass1 children, saves state →
/// <c>WaitPass1</c> → when all Pass1 done, dispatches Pass2 children, transitions to <c>WaitChildren</c> →
/// <c>WaitChildren</c> → <c>Finalize</c>.</para>
///
/// <para><b>Whole-task path:</b> coordinator resolves a single <see cref="EncodeTaskKind.Whole"/>
/// task and runs it inline — no coordinator state, no re-enqueue, matches original behavior.</para>
///
/// <para><b>No closure captures:</b> every phase wake-up reads everything it needs from
/// the database and the job payload. No <see cref="MediaContext"/> or object references
/// survive across <see cref="Handle"/> invocations.</para>
/// </summary>
public class VideoEncodeJob : AbstractEncoderJob, IJobIdReceiver
{
    public override string QueueName => "encoder";
    public override int Priority => 4;
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Durable phase state serialized into the job payload on every re-enqueue.
    /// Null on the initial run (no state yet). Presence of this field drives
    /// the state machine into the correct wake-up branch.
    /// </summary>
    public CoordinatorState? Coordinator { get; set; }

    private int _selfJobId;

    public void ReceiveJobId(int jobId) => _selfJobId = jobId;

    public override async Task Handle()
    {
        if (Coordinator is not null)
        {
            await HandleCoordinatorWakeUpAsync();
            return;
        }

        await HandleInitialRunAsync();
    }

    // ------------------------------------------------------------------
    // Initial run: decompose + dispatch (or inline for Whole tasks)
    // ------------------------------------------------------------------

    private async Task HandleInitialRunAsync()
    {
        await using MediaContext context = new();
        await using LibraryRepository libraryRepository = new(context, StorageDriver);
        FileRepository fileRepository = new(context, StorageDriver);
        FileManager fileManager = new(fileRepository, StorageFactory, StorageDriver);

        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
            return;

        List<EncodingPreset> presets = folder
            .EncodingPresetFolders.Where(link => link.Preset is not null)
            .Select(link => link.Preset!)
            .ToList();

        if (presets.Count == 0)
            return;

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
        if (!fileMetadata.Success)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();

        foreach (EncodingPreset preset in presets)
        {
            try
            {
                EncodingProfile encodingProfile;
                try
                {
                    encodingProfile = PresetResolver.Resolve(
                        preset.Id,
                        new DbPresetLookup(context)
                    );
                }
                catch (Exception ex)
                {
                    Logger.Encoder(
                        $"Skipping preset '{preset.Name}' ({preset.Id}): resolve failed — {ex.Message}",
                        LogEventLevel.Warning
                    );
                    continue;
                }

                if (encodingProfile.Video is null && encodingProfile.Audio.Length == 0)
                {
                    Logger.Encoder(
                        $"Skipping preset {preset.Name}: no video or audio outputs configured"
                    );
                    continue;
                }

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStartedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            OutputPath = fileMetadata.Path,
                            ProfileName = preset.Name,
                        }
                    );
                }

                IEncodingOrchestrator orchestrator =
                    EncoderProvider.ResolveService<IEncodingOrchestrator>()
                    ?? throw new InvalidOperationException(
                        "IEncodingOrchestrator is not registered. Did AddNoMercyEncoder() run?"
                    );

                IStorage destinationStorage = StorageFactory.For(
                    folder.Id,
                    folder.DriverId,
                    folder.Path
                );

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

                string groupTag = Ulid.NewUlid().ToString();
                DecomposedTask[] tasks = await orchestrator.DecomposeAsync(request, groupTag);

                bool isWhole = tasks.Length == 1 && tasks[0].Kind == EncodeTaskKind.Whole;

                if (isWhole)
                {
                    await RunInlineAsync(
                        orchestrator,
                        request,
                        encodingProfile,
                        preset,
                        fileMetadata,
                        stopwatch,
                        sourceStorage,
                        context,
                        fileManager,
                        folder
                    );
                    continue;
                }

                await DispatchDecomposedAsync(tasks, preset.Id, fileMetadata, stopwatch);
            }
            catch (Exception ex)
            {
                Logger.Encoder(ex, LogEventLevel.Error);

                if (EventBusProvider.IsConfigured)
                {
                    await EventBusProvider.Current.PublishAsync(
                        new EncodingStageChangedEvent
                        {
                            JobId = fileMetadata.Id,
                            Status = "failed",
                            Title = fileMetadata.Title,
                            Message = ex.Message,
                        }
                    );

                    await EventBusProvider.Current.PublishAsync(
                        new EncodingFailedEvent
                        {
                            JobId = fileMetadata.Id,
                            InputPath = InputFile,
                            ErrorMessage = ex.Message,
                            ExceptionType = ex.GetType().Name,
                        }
                    );
                }

                throw;
            }
        }
    }

    // ------------------------------------------------------------------
    // Coordinator state machine (subsequent wake-ups)
    // ------------------------------------------------------------------

    private async Task HandleCoordinatorWakeUpAsync()
    {
        CoordinatorState state = Coordinator!;

        Logger.Encoder(
            $"[VideoEncodeJob] Coordinator wake-up — GroupTag={state.GroupTag} Phase={state.Phase}"
        );

        switch (state.Phase)
        {
            case CoordinatorPhase.WaitPass1:
                await HandleWaitPass1Async(state);
                break;

            case CoordinatorPhase.WaitChildren:
                await HandleWaitChildrenAsync(state);
                break;

            case CoordinatorPhase.Finalize:
                await HandleFinalizeAsync(state);
                break;

            default:
                Logger.Encoder(
                    $"[VideoEncodeJob] Unknown coordinator phase '{state.Phase}' — completing job",
                    LogEventLevel.Warning
                );
                break;
        }
    }

    private async Task HandleWaitPass1Async(CoordinatorState state)
    {
        await using MediaContext context = new();

        string[] pass1TaskIds = state.TaskIds.Where(tid => tid.Contains("-pass1-")).ToArray();

        List<string> completedTaskIds = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .Where(o => o.GroupTag == state.GroupTag)
            .Select(o => o.TaskId)
            .ToListAsync();

        bool allPass1Done = pass1TaskIds.All(tid => completedTaskIds.Contains(tid));

        if (!allPass1Done)
        {
            int doneCount = pass1TaskIds.Count(tid => completedTaskIds.Contains(tid));
            Logger.Encoder(
                $"[VideoEncodeJob] WaitPass1: {doneCount}/{pass1TaskIds.Length} Pass1 tasks done — re-enqueueing"
            );
            ReEnqueueSelf(state with { Phase = CoordinatorPhase.WaitPass1 });
            return;
        }

        // All Pass1 tasks done — resolve stats file path and dispatch Pass2.
        EncodeTaskOutcome? anyPass1Outcome = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .FirstOrDefaultAsync(o =>
                o.GroupTag == state.GroupTag && o.Kind == nameof(EncodeTaskKind.Pass1)
            );

        string pass1StatsPath =
            anyPass1Outcome?.OutputArtifactsJson?.Split('\n').FirstOrDefault() ?? string.Empty;

        string[] pass2TaskIds = state.TaskIds.Where(tid => tid.Contains("-pass2-")).ToArray();

        string[] otherTaskIds = state
            .TaskIds.Where(tid => !tid.Contains("-pass1-") && !tid.Contains("-pass2-"))
            .ToArray();

        // Dispatch Pass2 children with the resolved stats file path.
        QueueJobDispatcher dispatcher = GetDispatcher();

        foreach (string pass2TaskId in pass2TaskIds)
        {
            int outputIndex = ParseOutputIndex(pass2TaskId);

            DecomposedTask pass2Task = new(
                TaskId: pass2TaskId,
                ParentJobId: _selfJobId,
                GroupTag: state.GroupTag,
                Kind: EncodeTaskKind.Pass2,
                OutputIndex: outputIndex,
                Resources: null,
                StatsFilePath: pass1StatsPath,
                Label: $"pass2 variant {outputIndex}"
            );

            EncodeTaskJob childJob = BuildChildJob(pass2Task, state.PresetId);
            dispatcher.DispatchChild(
                childJob,
                onQueue: childJob.QueueName,
                priority: childJob.Priority,
                parentJobId: _selfJobId,
                groupTag: state.GroupTag
            );
        }

        // Dispatch non-pass1/non-pass2 tasks that were held (audio, subtitle, thumbnails).
        foreach (string otherTaskId in otherTaskIds)
        {
            if (completedTaskIds.Contains(otherTaskId))
                continue;

            EncodeTaskKind kind = InferKindFromTaskId(otherTaskId);
            int outputIndex = ParseOutputIndex(otherTaskId);

            DecomposedTask otherTask = new(
                TaskId: otherTaskId,
                ParentJobId: _selfJobId,
                GroupTag: state.GroupTag,
                Kind: kind,
                OutputIndex: outputIndex,
                Resources: null,
                Label: otherTaskId
            );

            EncodeTaskJob childJob = BuildChildJob(otherTask, state.PresetId);
            dispatcher.DispatchChild(
                childJob,
                onQueue: childJob.QueueName,
                priority: childJob.Priority,
                parentJobId: _selfJobId,
                groupTag: state.GroupTag
            );
        }

        Logger.Encoder(
            $"[VideoEncodeJob] WaitPass1 complete — dispatched {pass2TaskIds.Length} Pass2 + {otherTaskIds.Length} other tasks. Transitioning to WaitChildren."
        );

        ReEnqueueSelf(
            state with
            {
                Phase = CoordinatorPhase.WaitChildren,
                Pass1StatsPath = pass1StatsPath,
                Pass2DispatchedAt = DateTime.UtcNow,
            }
        );
    }

    private async Task HandleWaitChildrenAsync(CoordinatorState state)
    {
        await using MediaContext context = new();

        List<string> completedTaskIds = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .Where(o => o.GroupTag == state.GroupTag)
            .Select(o => o.TaskId)
            .ToListAsync();

        // Filter: only non-pass1 tasks count toward completion in WaitChildren.
        string[] nonPass1TaskIds = state.TaskIds.Where(tid => !tid.Contains("-pass1-")).ToArray();

        bool allDone = nonPass1TaskIds.All(tid => completedTaskIds.Contains(tid));

        if (!allDone)
        {
            int doneCount = nonPass1TaskIds.Count(tid => completedTaskIds.Contains(tid));
            Logger.Encoder(
                $"[VideoEncodeJob] WaitChildren: {doneCount}/{nonPass1TaskIds.Length} tasks done — re-enqueueing"
            );
            ReEnqueueSelf(state with { Phase = CoordinatorPhase.WaitChildren });
            return;
        }

        Logger.Encoder(
            $"[VideoEncodeJob] WaitChildren complete — all tasks done. Transitioning to Finalize."
        );
        ReEnqueueSelf(state with { Phase = CoordinatorPhase.Finalize });
    }

    private async Task HandleFinalizeAsync(CoordinatorState state)
    {
        // Open a fresh MediaContext for finalize — nothing is captured from
        // any prior Handle() invocation. This is the key B1 fix.
        await using MediaContext context = new();

        FileRepository fileRepository = new(context, StorageDriver);
        FileManager fileManager = new(fileRepository, StorageFactory, StorageDriver);

        await using LibraryRepository libraryRepository = new(context, StorageDriver);
        Folder? folder = await libraryRepository.GetLibraryFolder(FolderId);
        if (folder is null)
        {
            Logger.Encoder(
                $"[VideoEncodeJob] Finalize: folder {FolderId} not found — aborting post-encode",
                LogEventLevel.Warning
            );
            return;
        }

        FileMetadata fileMetadata = await GetFileMetaData(folder, context);
        if (!fileMetadata.Success)
            return;

        List<EncodeTaskOutcome> outcomes = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .Where(o => o.GroupTag == state.GroupTag)
            .ToListAsync();

        int failedCount = outcomes.Count(o => !o.Success);

        if (failedCount > 0)
        {
            Logger.Encoder(
                $"[VideoEncodeJob] Finalize: {failedCount} task(s) failed — skipping post-encode",
                LogEventLevel.Warning
            );

            if (EventBusProvider.IsConfigured)
            {
                await EventBusProvider.Current.PublishAsync(
                    new EncodingStageChangedEvent
                    {
                        JobId = fileMetadata.Id,
                        Status = "failed",
                        Title = fileMetadata.Title,
                        Message = $"{failedCount} rung(s) failed",
                    }
                );
            }

            return;
        }

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(SourceDriverId.Value, SourceDriverId.Value, string.Empty)
            : StorageFactory.For(folder.Id, folder.DriverId, folder.Path);

        IStorage destinationStorage = StorageFactory.For(folder.Id, folder.DriverId, folder.Path);

        // Coordinator finalize: run the pipeline once over the shared cache
        // tempDir with Options.FinalizeOnly=true. The orchestrator skips
        // Build + Execute, runs FinalizeStage against the variant playlists
        // the children produced (writing master m3u8, chapters.vtt,
        // fonts.json, manifest.json), then publishes the whole cache to
        // the destination and deletes it. Per-task EncodeAsync runs skip
        // FinalizeStage + publish + cleanup precisely to avoid racing this
        // single coordinator-driven pass.
        IEncodingOrchestrator orchestrator =
            EncoderProvider.ResolveService<IEncodingOrchestrator>()
            ?? throw new InvalidOperationException(
                "IEncodingOrchestrator is not registered. Did AddNoMercyEncoder() run?"
            );

        EncodingProfile finalizeProfile;
        await using (MediaContext profileLookup = new())
        {
            try
            {
                finalizeProfile = PresetResolver.Resolve(
                    state.PresetId,
                    new DbPresetLookup(profileLookup)
                );
            }
            catch (Exception ex)
            {
                Logger.Encoder(
                    $"[VideoEncodeJob] Finalize: cannot resolve preset {state.PresetId} — {ex.Message}",
                    LogEventLevel.Warning
                );
                return;
            }
        }

        EncodingRequest finalizeRequest = new(
            InputPath: InputFile,
            OutputDirectory: fileMetadata.Path,
            Profile: finalizeProfile,
            Options: new(FinalizeOnly: true),
            MediaTitle: fileMetadata.FileName,
            SourceStorage: sourceStorage,
            DestinationStorage: destinationStorage
        );

        await PublishStageAsync(fileMetadata, "Publishing artifacts");
        try
        {
            EncodingResult publishResult = await orchestrator.EncodeAsync(finalizeRequest);
            if (!publishResult.Success)
            {
                string err =
                    publishResult.Error?.Message
                    ?? publishResult.EnrichedError?.Message
                    ?? "finalize-only pass failed with no details";
                Logger.Encoder(
                    $"[VideoEncodeJob] Coordinator finalize failed for GroupTag={state.GroupTag}: {err}",
                    LogEventLevel.Error
                );
                return;
            }
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"[VideoEncodeJob] Coordinator finalize threw for GroupTag={state.GroupTag}: {ex.Message}",
                LogEventLevel.Error
            );
            throw;
        }

        await PublishStageAsync(fileMetadata, "Checking source subtitles");
        await RunBitmapSubtitleOcrAsync(fileMetadata, InputFile, sourceStorage);

        await PublishStageAsync(fileMetadata, "Refreshing library");
        Library library = folder.FolderLibraries.First().Library;
        fileManager.FilterFiles(fileMetadata.FileName);
        await fileManager.FindFiles(fileMetadata.Id, library);

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new EncodingCompletedEvent
                {
                    JobId = fileMetadata.Id,
                    OutputPath = fileMetadata.Path,
                    Duration = TimeSpan.Zero,
                }
            );
        }

        Logger.Encoder($"[VideoEncodeJob] Finalize complete for GroupTag={state.GroupTag}");
    }

    // ------------------------------------------------------------------
    // Dispatch helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Dispatches all decomposed children and re-enqueues the coordinator with
    /// phase state so it can poll for completion after a restart.
    /// </summary>
    private async Task DispatchDecomposedAsync(
        DecomposedTask[] tasks,
        Ulid presetId,
        FileMetadata fileMetadata,
        Stopwatch stopwatch
    )
    {
        int parentJobId = _selfJobId;
        string groupTag = tasks[0].GroupTag;

        Logger.Encoder(
            $"[VideoEncodeJob] Decomposed into {tasks.Length} child tasks (groupTag={groupTag})"
        );

        QueueJobDispatcher dispatcher = GetDispatcher();

        bool hasTwoPass = tasks.Any(task =>
            task.Kind == EncodeTaskKind.Pass1 || task.Kind == EncodeTaskKind.Pass2
        );

        string[] allTaskIds = tasks.Select(task => task.TaskId).ToArray();

        if (hasTwoPass)
        {
            // Two-pass: dispatch only Pass1 tasks first. Pass2 and aux are dispatched
            // by the coordinator after all Pass1 tasks complete.
            DecomposedTask[] pass1Tasks = tasks
                .Where(task => task.Kind == EncodeTaskKind.Pass1)
                .ToArray();

            foreach (DecomposedTask task in pass1Tasks)
            {
                DecomposedTask stamped = task with { ParentJobId = parentJobId };
                EncodeTaskJob childJob = BuildChildJob(stamped, presetId);
                dispatcher.DispatchChild(
                    childJob,
                    onQueue: childJob.QueueName,
                    priority: childJob.Priority,
                    parentJobId: parentJobId,
                    groupTag: groupTag
                );
            }

            Logger.Encoder(
                $"[VideoEncodeJob] Dispatched {pass1Tasks.Length} Pass1 tasks. Transitioning to WaitPass1."
            );

            ReEnqueueSelf(
                new CoordinatorState(
                    GroupTag: groupTag,
                    TaskIds: allTaskIds,
                    Phase: CoordinatorPhase.WaitPass1,
                    Pass1DispatchedAt: DateTime.UtcNow,
                    Pass2DispatchedAt: null,
                    Pass1StatsPath: null,
                    PresetId: presetId,
                    ExpectedFinalCount: tasks.Count(task => task.Kind != EncodeTaskKind.Pass1)
                )
            );
        }
        else
        {
            // Single-pass: dispatch all tasks immediately.
            foreach (DecomposedTask task in tasks)
            {
                DecomposedTask stamped = task with { ParentJobId = parentJobId };
                EncodeTaskJob childJob = BuildChildJob(stamped, presetId);
                dispatcher.DispatchChild(
                    childJob,
                    onQueue: childJob.QueueName,
                    priority: childJob.Priority,
                    parentJobId: parentJobId,
                    groupTag: groupTag
                );
            }

            Logger.Encoder(
                $"[VideoEncodeJob] Dispatched {tasks.Length} child tasks. Transitioning to WaitChildren."
            );

            ReEnqueueSelf(
                new CoordinatorState(
                    GroupTag: groupTag,
                    TaskIds: allTaskIds,
                    Phase: CoordinatorPhase.WaitChildren,
                    Pass1DispatchedAt: null,
                    Pass2DispatchedAt: null,
                    Pass1StatsPath: null,
                    PresetId: presetId,
                    ExpectedFinalCount: tasks.Length
                )
            );
        }

        await Task.CompletedTask;
        _ = fileMetadata;
        _ = stopwatch;
    }

    /// <summary>
    /// Re-enqueues the coordinator as a new job with updated <see cref="CoordinatorState"/>.
    /// The current job instance is deleted by the worker after <see cref="Handle"/> returns.
    /// The new job has a different payload (new Phase), so the deduplication check passes.
    /// </summary>
    private void ReEnqueueSelf(CoordinatorState newState)
    {
        VideoEncodeJob continueJob = new()
        {
            Id = Id,
            FolderId = FolderId,
            LibraryId = LibraryId,
            InputFile = InputFile,
            SourceDriverId = SourceDriverId,
            Status = "running",
            Coordinator = newState,
        };

        QueueJobDispatcher dispatcher = GetDispatcher();
        JobQueue queue =
            QueueRunner.Current?.Queue
            ?? throw new InvalidOperationException(
                "QueueRunner.Current.Queue is null — queue not initialized"
            );

        // Dispatch as a new top-level coordinator job (no parent ID).
        // The different Coordinator payload means deduplication won't block it.
        queue.Enqueue(
            new NoMercyQueue.Core.Models.QueueJobModel
            {
                Queue = QueueName,
                Payload = SerializationHelper.Serialize(continueJob),
                Priority = Priority,
                AvailableAt = DateTime.UtcNow + TimeSpan.FromSeconds(5),
            }
        );

        Logger.Encoder(
            $"[VideoEncodeJob] Re-enqueued coordinator with Phase={newState.Phase}, GroupTag={newState.GroupTag}"
        );
    }

    private EncodeTaskJob BuildChildJob(DecomposedTask task, Ulid presetId)
    {
        return new()
        {
            LibraryId = LibraryId,
            FolderId = FolderId,
            Id = Id,
            InputFile = InputFile,
            SourceDriverId = SourceDriverId,
            PresetId = presetId,
            Task = task,
        };
    }

    private static QueueJobDispatcher GetDispatcher()
    {
        return QueueRunner.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                "QueueRunner.Current is null — queue not initialized"
            );
    }

    // ------------------------------------------------------------------
    // Inline path for Whole tasks (MP4, MKV — unchanged behavior)
    // ------------------------------------------------------------------

    private async Task RunInlineAsync(
        IEncodingOrchestrator orchestrator,
        EncodingRequest request,
        EncodingProfile encodingProfile,
        EncodingPreset preset,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        MediaContext context,
        FileManager fileManager,
        Folder folder
    )
    {
        IEncoderProcessRegistry? processRegistry =
            EncoderProvider.ResolveService<IEncoderProcessRegistry>();

        EventBusProgressObserver progressObserver = new(
            jobId: fileMetadata.Id,
            title: fileMetadata.Title,
            baseFolder: fileMetadata.Path,
            sharePath: fileMetadata.Path,
            videoStreams: SummarizeVideo(encodingProfile),
            audioStreams: encodingProfile
                .Audio.Select(audio =>
                    $"{audio.Codec.ToString().ToLowerInvariant()} {audio.Channels}ch"
                )
                .ToList(),
            subtitleStreams: encodingProfile
                .Subtitles.Select(subtitle => subtitle.Codec.ToString().ToLowerInvariant())
                .ToList(),
            hasGpu: false,
            isHdr: false,
            registry: processRegistry
        );

        EncodingResult result = await orchestrator.EncodeAsync(request, progressObserver);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Encoding failed for {InputFile}: {result.Error?.Message ?? "unknown error"}"
            );
        }

        Logger.Encoder(
            $"Encoded {InputFile} → {result.OutputPath} in {result.Duration.TotalSeconds:F1}s ({result.Metrics?.EncoderUsed ?? "unknown"})"
        );

        await PublishStageAsync(fileMetadata, "Recording encoding history");
        await RecordEncodingHistoryAsync(context, preset, result, InputFile, StorageDriver);

        await PublishStageAsync(fileMetadata, "Checking source subtitles");
        await RunBitmapSubtitleOcrAsync(fileMetadata, InputFile, sourceStorage);

        await PublishStageAsync(fileMetadata, "Refreshing library");
        fileManager.FilterFiles(fileMetadata.FileName);
        await fileManager.FindFiles(fileMetadata.Id, folder.FolderLibraries.First().Library);

        if (EventBusProvider.IsConfigured)
        {
            stopwatch.Stop();
            await EventBusProvider.Current.PublishAsync(
                new EncodingCompletedEvent
                {
                    JobId = fileMetadata.Id,
                    OutputPath = result.OutputPath,
                    Duration = stopwatch.Elapsed,
                }
            );
        }
    }

    // ------------------------------------------------------------------
    // Utility / helpers
    // ------------------------------------------------------------------

    private static List<string> SummarizeVideo(EncodingProfile profile)
    {
        List<string> summary = [];

        if (profile.Ladder?.Rungs is { Length: > 0 } rungs)
        {
            foreach (LadderRung rung in rungs)
                summary.Add(
                    $"{rung.Width}x{rung.Height} {rung.Codec.ToString().ToLowerInvariant()}"
                );
            return summary;
        }

        if (profile.Video is { } video)
            summary.Add(
                $"{video.Width}x{video.Height ?? 0} {video.Codec.ToString().ToLowerInvariant()}"
            );

        return summary;
    }

    private static async Task RecordEncodingHistoryAsync(
        MediaContext context,
        EncodingPreset preset,
        EncodingResult result,
        string inputPath,
        IStorageDriver storageDriver
    )
    {
        try
        {
            long inputSize = 0;
            try
            {
                if (storageDriver.FileExists(inputPath))
                    inputSize = storageDriver.GetFileSize(inputPath);
            }
            catch
            {
                // keep inputSize = 0 when the file is inaccessible
            }

            if (result.Metrics is null)
                return;

            double ratio =
                inputSize > 0 && result.Metrics.OutputSizeBytes > 0
                    ? (double)result.Metrics.OutputSizeBytes / inputSize
                    : 0;

            context.EncodingHistory.Add(
                new()
                {
                    InputPath = inputPath,
                    OutputPath = result.OutputPath,
                    ProfileId = preset.Id,
                    ProfileName = preset.Name,
                    EncoderUsed = result.Metrics.EncoderUsed,
                    GpuUsed = result.Metrics.GpuUsed,
                    DurationSeconds = result.Duration.TotalSeconds,
                    InputSizeBytes = inputSize,
                    OutputSizeBytes = result.Metrics.OutputSizeBytes,
                    CompressionRatio = ratio,
                    AverageSpeed = result.Metrics.AverageSpeed,
                    AverageFps = result.Metrics.AverageFps,
                }
            );
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"Could not write encoding history: {ex.Message}",
                LogEventLevel.Warning
            );
        }
    }

    private async Task RunBitmapSubtitleOcrAsync(
        FileMetadata fileMetadata,
        string inputPath,
        IStorage sourceStorage
    )
    {
        IMediaAnalyzer? analyzer = EncoderProvider.ResolveService<IMediaAnalyzer>();
        ISubtitleOcrEngine? ocrEngine = EncoderProvider.ResolveService<ISubtitleOcrEngine>();

        if (analyzer is null || ocrEngine is null)
            return;

        MediaInfo mediaInfo;
        try
        {
            mediaInfo = await analyzer.AnalyzeAsync(
                inputPath,
                sourceStorage,
                CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Logger.Encoder(
                $"Could not analyze {inputPath} for OCR: {ex.Message}",
                LogEventLevel.Warning
            );
            return;
        }

        List<SubtitleStreamInfo> bitmap = mediaInfo
            .SubtitleStreams.Where(subtitle => !subtitle.IsTextBased)
            .ToList();

        if (bitmap.Count == 0)
            return;

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                new EncodingStageChangedEvent
                {
                    JobId = fileMetadata.Id,
                    Status = "running",
                    Title = fileMetadata.Title,
                    Message = "Converting subtitles",
                }
            );
        }

        foreach (SubtitleStreamInfo stream in bitmap)
        {
            string language = stream.Language ?? "eng";
            try
            {
                SubtitleTrack track = await ocrEngine.OcrAsync(
                    inputPath,
                    stream.Index,
                    language,
                    SubtitleCodecType.WebVtt,
                    CancellationToken.None
                );
                Logger.Encoder($"OCR {language} → {track.FilePath} ({track.CueCount} cues)");
            }
            catch (Exception ex)
            {
                Logger.Encoder(
                    $"OCR failed for {inputPath} stream {stream.Index} ({language}): {ex.Message}",
                    LogEventLevel.Warning
                );
            }
        }
    }

    private async Task<FileMetadata> GetFileMetaData(Folder folder, MediaContext context)
    {
        Movie? movie = folder.FolderLibraries.Any(x => x.Library.Type == Config.MovieMediaType)
            ? await context.Movies.FirstOrDefaultAsync(x => x.Id == Id.ToInt())
            : null;

        Episode? episode = folder.FolderLibraries.Any(x =>
            x.Library.Type == Config.TvMediaType || x.Library.Type == Config.AnimeMediaType
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
        string? imgPath = movie?.Backdrop ?? episode?.Still;

        return new()
        {
            Success = true,
            FolderName = folderName,
            Title = title,
            FileName = fileName,
            Path = basePath,
            Id = baseId,
            ImgPath = imgPath,
        };
    }

    // ------------------------------------------------------------------
    // Task-ID parsing helpers
    // ------------------------------------------------------------------

    private static int ParseOutputIndex(string taskId)
    {
        int dashIndex = taskId.LastIndexOf('-');
        if (dashIndex >= 0 && int.TryParse(taskId.AsSpan(dashIndex + 1), out int index))
            return index;
        return 0;
    }

    private static EncodeTaskKind InferKindFromTaskId(string taskId)
    {
        if (taskId.Contains("-audio-"))
            return EncodeTaskKind.Audio;
        if (taskId.Contains("-sub-"))
            return EncodeTaskKind.Subtitle;
        if (taskId.Contains("-thumbs"))
            return EncodeTaskKind.Thumbnails;
        if (taskId.Contains("-video-"))
            return EncodeTaskKind.Video;
        return EncodeTaskKind.Video;
    }

    private record FileMetadata
    {
        public bool Success { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public int Id { get; set; }
        public string? ImgPath { get; set; }
    }

    private static async Task PublishStageAsync(FileMetadata fileMetadata, string message)
    {
        if (!EventBusProvider.IsConfigured)
            return;
        await EventBusProvider.Current.PublishAsync(
            new EncodingStageChangedEvent
            {
                JobId = fileMetadata.Id,
                Status = "running",
                Title = fileMetadata.Title,
                Message = message,
            }
        );
    }
}
