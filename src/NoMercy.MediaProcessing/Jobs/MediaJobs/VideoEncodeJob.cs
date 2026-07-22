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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoMercy.Database;
using NoMercy.Database.Models.Encoder;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Reconciliation;
using NoMercy.Encoder.Subtitles;
using NoMercy.Events;
using NoMercy.Events.Encoding;
using NoMercy.MediaProcessing.Files;
using NoMercy.MediaProcessing.Jobs.MediaJobs.Support;
using NoMercy.MediaProcessing.Libraries;
using NoMercy.NmSystem.Domain;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage;
using NoMercyQueue;
using NoMercyQueue.Core.Interfaces;
using Serilog.Events;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;
using MediaType = NoMercy.Encoder.Naming.MediaType;
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
public class VideoEncodeJob : AbstractEncoderJob, IJobIdReceiver, IJobStorageInjector
{
    private IEncodingOrchestrator? _encodingOrchestrator;
    private IHardwareBenchmark? _hardwareBenchmark;
    private IHardwareCapabilities? _hardwareCapabilities;
    private IEncoderProcessRegistry? _encoderProcessRegistry;
    private IMediaAnalyzer? _mediaAnalyzer;
    private ISubtitleOcrEngine? _subtitleOcrEngine;
    private IEncodeReconciler? _encodeReconciler;

    // Host shutdown signal for the post-encode scan retry backoff — falls back to
    // CancellationToken.None when the DI scope has no IHostApplicationLifetime
    // (e.g. a minimal test scope) so the retry loop still runs, just without an
    // early-exit on server shutdown.
    private CancellationToken _shutdownToken;

    public new void InjectStorageServices(IServiceProvider serviceProvider)
    {
        base.InjectStorageServices(serviceProvider: serviceProvider);
        _encodingOrchestrator = serviceProvider.GetRequiredService<IEncodingOrchestrator>();
        _hardwareBenchmark = serviceProvider.GetRequiredService<IHardwareBenchmark>();
        _hardwareCapabilities = serviceProvider.GetRequiredService<IHardwareCapabilities>();
        _encoderProcessRegistry = serviceProvider.GetRequiredService<IEncoderProcessRegistry>();
        _mediaAnalyzer = serviceProvider.GetRequiredService<IMediaAnalyzer>();
        _subtitleOcrEngine = serviceProvider.GetRequiredService<ISubtitleOcrEngine>();
        _encodeReconciler = serviceProvider.GetRequiredService<IEncodeReconciler>();
        _shutdownToken =
            serviceProvider.GetService<IHostApplicationLifetime>()?.ApplicationStopping
            ?? CancellationToken.None;
    }

    public override string QueueName => "encoder";
    public override int Priority => 4;
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Durable phase state serialized into the job payload on every re-enqueue.
    /// Null on the initial run (no state yet). Presence of this field drives
    /// the state machine into the correct wake-up branch.
    /// </summary>
    public CoordinatorState? Coordinator { get; set; }

    /// <summary>
    /// When set, encoding is limited to the single preset with this id.
    /// Null preserves the default all-presets behavior.
    /// </summary>
    public Ulid? PresetId { get; set; }

    /// <summary>
    /// Operator escape hatch: when true, reconciliation is skipped entirely
    /// and every preset is fully re-encoded, regardless of what is already
    /// on disk or whether its profile fingerprint matches. Defaults to false
    /// so every existing serialized job (and every dashboard "redispatch")
    /// keeps today's reconciled behavior without needing to set anything.
    /// </summary>
    public bool ForceFullReencode { get; set; }

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
        await using LibraryRepository libraryRepository = new(context: context, storageDriver: StorageDriver);
        FileRepository fileRepository = new(context: context, storageDriver: StorageDriver);
        FileManager fileManager = new(
            fileRepository: fileRepository,
            storageFactory: StorageFactory,
            storageDriver: StorageDriver,
            mediaAnalyzer: _mediaAnalyzer!
        );

        Folder? folder = await libraryRepository.GetLibraryFolder(folderId: FolderId);
        if (folder is null)
            return;

        List<EncodingPreset> presets = folder
            .EncodingPresetFolders.Where(predicate: link => link.Preset is not null)
            .Select(selector: link => link.Preset!)
            .ToList();

        if (PresetId is not null)
        {
            presets = presets.Where(predicate: preset => preset.Id == PresetId.Value).ToList();
            if (presets.Count == 0)
                Log.LogWarning(
                    message: "[VideoEncodeJob] PresetId {Value} not found in folder {FolderId} — no presets to run", args: [PresetId.Value, FolderId]
                );
        }

        if (presets.Count == 0)
            return;

        FileMetadata fileMetadata = await GetFileMetaData(folder: folder, context: context);
        if (!fileMetadata.Success)
            return;

        Stopwatch stopwatch = Stopwatch.StartNew();

        IStorage destinationStorage = StorageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(folderId: SourceDriverId.Value, driverId: SourceDriverId.Value, subPath: string.Empty)
            : destinationStorage;

        // Resolve every selected preset and reconcile it against what's
        // already on disk BEFORE dispatching anything. This is what lets a
        // folder with several presets (e.g. "4K HDR HEVC" + "1080p SDR HEVC")
        // become ONE coordinated encode below instead of one independent
        // VideoEncodeJob-style run per preset — the bug this state machine
        // used to have when the per-preset foreach dispatched immediately.
        List<PlannedPreset> planned = [];

        try
        {
            foreach (EncodingPreset preset in presets)
            {
                EncodingProfile encodingProfile;
                try
                {
                    encodingProfile = PresetResolver.Resolve(
                        presetId: preset.Id,
                        lookup: new DbPresetLookup(context: context)
                    );
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        message: "Skipping preset '{Name}' ({Id}): resolve failed — {Message}", args: [preset.Name, preset.Id, ex.Message]
                    );
                    continue;
                }

                if (encodingProfile.Video is null && encodingProfile.Audio.Length == 0)
                {
                    Log.LogWarning(
                        message: "Skipping preset {Name}: no video or audio outputs configured",
                        args: preset.Name
                    );
                    continue;
                }

                ReconciliationDecision reconciliation = await ReconcileAsync(
                    encodingProfile: encodingProfile,
                    fileMetadata: fileMetadata,
                    sourceStorage: sourceStorage,
                    destinationStorage: destinationStorage
                );

                if (reconciliation.Action == ReconciliationAction.Skip)
                {
                    Log.LogInformation(
                        message: "[VideoEncodeJob] Reconciliation: skipping preset '{Name}' for {Id} — {Reason}", args: [preset.Name, fileMetadata.Id, reconciliation.Reason]
                    );
                    continue;
                }

                planned.Add(item: new(Preset: preset, Profile: encodingProfile, Decision: reconciliation));
            }
        }
        catch (Exception ex)
        {
            Log.LogError(exception: ex, message: "Video encode task failed");

            await EncoderCardTerminator.PublishFailedAsync(
                jobId: fileMetadata.Id,
                title: fileMetadata.Title,
                inputPath: InputFile,
                message: ex.Message,
                exceptionType: ex.GetType().Name
            );

            throw;
        }

        if (planned.Count == 0)
            return;

        // OCR-only top-ups (every real output already valid, only the
        // bitmap-subtitle sidecar missing) run ONCE for the whole batch —
        // the sidecar is source-derived, identical no matter which preset
        // asked for it. Running it once per preset was the double-OCR half
        // of the bug this coordinated-encode work fixes.
        List<PlannedPreset> ocrOnly = planned
            .Where(predicate: entry =>
                entry.Decision is { Action: ReconciliationAction.Partial, MissingKinds.Count: 0 }
            )
            .ToList();

        if (ocrOnly.Count > 0)
        {
            foreach (PlannedPreset entry in ocrOnly)
                Log.LogInformation(
                    message: "[VideoEncodeJob] Reconciliation: preset '{Name}' for {Id} is fully encoded — running OCR top-up only ({Reason})", args: [entry.Preset.Name, fileMetadata.Id, entry.Decision.Reason]
                );

            await RunOcrTopUpAsync(
                fileMetadata: fileMetadata,
                sourceStorage: sourceStorage,
                destinationStorage: destinationStorage,
                fileManager: fileManager,
                folder: folder
            );
        }

        List<PlannedPreset> needsWork = planned.Except(second: ocrOnly).ToList();
        if (needsWork.Count == 0)
            return;

        // A merged run needs one consistent reconciliation verdict across
        // every preset it covers: a Partial top-up must not rewrite the
        // master (it only fills gaps), so mixing it with a Full re-encode
        // — which must rewrite the master to list every rendition — has no
        // single correct answer. A single preset trivially qualifies (its
        // own verdict is the only one that matters); several presets only
        // qualify when they all agree the whole thing needs a fresh encode.
        bool canMerge =
            needsWork.Count == 1
            || needsWork.All(predicate: entry => entry.Decision.Action == ReconciliationAction.Full);

        try
        {
            if (canMerge)
            {
                try
                {
                    await RunMergedEncodeAsync(
                        needsWork: needsWork,
                        fileMetadata: fileMetadata,
                        stopwatch: stopwatch,
                        sourceStorage: sourceStorage,
                        destinationStorage: destinationStorage,
                        context: context,
                        fileManager: fileManager,
                        folder: folder
                    );
                    return;
                }
                catch (MergedEncodingIncompatibleException ex)
                {
                    Log.LogWarning(
                        message: "[VideoEncodeJob] Merged decompose unavailable for folder {FolderId} ({Reason}) — falling back to independent per-preset dispatch for {Count} preset(s).", args: [FolderId, ex.Message, needsWork.Count]
                    );
                }
            }

            foreach (PlannedPreset entry in needsWork)
            {
                await RunSinglePresetEncodeAsync(
                    preset: entry.Preset,
                    encodingProfile: entry.Profile,
                    reconciliation: entry.Decision,
                    fileMetadata: fileMetadata,
                    stopwatch: stopwatch,
                    sourceStorage: sourceStorage,
                    destinationStorage: destinationStorage,
                    context: context,
                    fileManager: fileManager,
                    folder: folder
                );
            }
        }
        catch (Exception ex)
        {
            Log.LogError(exception: ex, message: "Video encode task failed");

            await EncoderCardTerminator.PublishFailedAsync(
                jobId: fileMetadata.Id,
                title: fileMetadata.Title,
                inputPath: InputFile,
                message: ex.Message,
                exceptionType: ex.GetType().Name
            );

            throw;
        }
    }

    /// <summary>
    /// One preset resolved to a profile and reconciled against what's already
    /// on disk, still waiting to be dispatched. Bridges the resolve/reconcile
    /// pass in <see cref="HandleInitialRunAsync"/> to the merged and
    /// per-preset dispatch paths below it.
    /// </summary>
    private sealed record PlannedPreset(
        EncodingPreset Preset,
        EncodingProfile Profile,
        ReconciliationDecision Decision
    );

    /// <summary>
    /// The smart-orchestrator path: builds one <see cref="EncodingRequest"/>
    /// per preset in <paramref name="needsWork"/> and asks the orchestrator to
    /// decompose them as ONE coordinated encode — a single output folder, a
    /// single master playlist listing every preset's video rendition, with
    /// audio / subtitles / thumbnails / chapters produced once and shared.
    /// Throws <see cref="MergedEncodingIncompatibleException"/> (propagated
    /// from <see cref="IEncodingOrchestrator.DecomposeMergedAsync"/>, or
    /// raised here when a merged decompose resolves to a single Whole task
    /// for more than one preset — a single-file container can only ever hold
    /// one preset's output) so the caller can fall back to dispatching each
    /// preset independently.
    /// </summary>
    private async Task RunMergedEncodeAsync(
        List<PlannedPreset> needsWork,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        IStorage destinationStorage,
        MediaContext context,
        FileManager fileManager,
        Folder folder
    )
    {
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingStartedEvent
                {
                    JobId = fileMetadata.Id,
                    InputPath = InputFile,
                    OutputPath = fileMetadata.Path,
                    ProfileName = string.Join(separator: " + ", values: needsWork.Select(selector: entry => entry.Preset.Name)),
                }
            );
        }

        IEncodingOrchestrator orchestrator = _encodingOrchestrator!;

        List<EncodingRequest> requests = needsWork
            .Select(selector: entry => new EncodingRequest(
                InputPath: InputFile,
                OutputDirectory: fileMetadata.Path,
                Profile: entry.Profile,
                MediaTitle: fileMetadata.FileName,
                SourceStorage: sourceStorage,
                DestinationStorage: destinationStorage,
                // Same identity every request in the run carries — see the
                // matching comment in RunSinglePresetEncodeAsync.
                MediaItem: fileMetadata.MediaItem
            ))
            .ToList();

        string groupTag = Ulid.NewUlid().ToString();
        (OutputPlan? plan, DecomposedTask[] tasks) =
            await orchestrator.DecomposeMergedWithPlanAsync(requests: requests, groupTag: groupTag);

        // A partial top-up only ever applies when the merge covers exactly
        // one preset — canMerge in HandleInitialRunAsync requires every
        // OTHER member of a multi-preset merge to be a clean Full re-encode,
        // since a top-up must not rewrite the master and a Full run must.
        bool isPartialTopUp = false;
        if (
            needsWork is [{ Decision.Action: ReconciliationAction.Partial }]
            && needsWork[index: 0].Decision.MissingKinds.Count > 0
        )
        {
            ReconciliationDecision decision = needsWork[index: 0].Decision;
            tasks = tasks.Where(predicate: task => decision.MissingKinds.Contains(value: task.Kind)).ToArray();
            isPartialTopUp = true;

            if (tasks.Length == 0)
            {
                Log.LogWarning(
                    message: "[VideoEncodeJob] Reconciliation flagged {Kinds} as missing for preset '{Name}' but decomposition produced no matching task — falling back to a full re-encode", args: [string.Join(separator: ", ", values: decision.MissingKinds), needsWork[index: 0].Preset.Name]
                );
                (plan, tasks) = await orchestrator.DecomposeMergedWithPlanAsync(requests: requests, groupTag: groupTag);
                isPartialTopUp = false;
            }
        }

        bool isWhole = tasks is [{ Kind: EncodeTaskKind.Whole }];

        if (isWhole)
        {
            if (needsWork.Count > 1)
                throw new MergedEncodingIncompatibleException(
                    message: "Merged decompose produced a single Whole (single-file container) task "
                             + $"for {needsWork.Count} presets — a single-file output can only ever "
                             + "hold one preset's encode."
                );

            await RunInlineAsync(
                orchestrator: orchestrator,
                request: requests[index: 0],
                encodingProfile: needsWork[index: 0].Profile,
                preset: needsWork[index: 0].Preset,
                fileMetadata: fileMetadata,
                stopwatch: stopwatch,
                sourceStorage: sourceStorage,
                context: context,
                fileManager: fileManager,
                folder: folder
            );
            return;
        }

        // Non-Whole tasks only ever come back from a plan that decomposed
        // successfully — DecomposeMergedWithPlanAsync's null Plan cases both
        // collapse to the single Whole fallback handled above, so this can
        // never actually fire; it documents the invariant instead of casting
        // it away with a null-forgiving operator.
        OutputPlan resolvedPlan =
            plan
            ?? throw new InvalidOperationException(
                message: "DecomposeMergedWithPlanAsync returned decomposed tasks without a Plan — "
                         + "the decode-aware bundler cannot classify outputs without it."
            );

        Ulid[] presetIds = needsWork.Select(selector: entry => entry.Preset.Id).ToArray();
        await DispatchDecomposedAsync(
            tasks: tasks,
            plan: resolvedPlan,
            presetIds: presetIds,
            fileMetadata: fileMetadata,
            stopwatch: stopwatch,
            isPartialTopUp: isPartialTopUp
        );
    }

    /// <summary>
    /// The legacy path, preserved verbatim for callers that can't merge: a
    /// single selected preset (the common case — most folders carry exactly
    /// one), and the rare fallback when <see cref="RunMergedEncodeAsync"/>
    /// throws <see cref="MergedEncodingIncompatibleException"/> or when
    /// several presets disagree on what reconciliation needs to do. Each
    /// preset gets its own independent encode, exactly as every
    /// <see cref="VideoEncodeJob"/> run did before the smart orchestrator.
    /// </summary>
    private async Task RunSinglePresetEncodeAsync(
        EncodingPreset preset,
        EncodingProfile encodingProfile,
        ReconciliationDecision reconciliation,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        IStorage sourceStorage,
        IStorage destinationStorage,
        MediaContext context,
        FileManager fileManager,
        Folder folder
    )
    {
        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingStartedEvent
                {
                    JobId = fileMetadata.Id,
                    InputPath = InputFile,
                    OutputPath = fileMetadata.Path,
                    ProfileName = preset.Name,
                }
            );
        }

        IEncodingOrchestrator orchestrator = _encodingOrchestrator!;

        EncodingRequest request = new(
            InputPath: InputFile,
            OutputDirectory: fileMetadata.Path,
            Profile: encodingProfile,
            MediaTitle: fileMetadata.FileName,
            SourceStorage: sourceStorage,
            DestinationStorage: destinationStorage,
            // Pure identity — safe on every request, including the Whole-task
            // inline path below (RunInlineAsync runs Build+Execute+Finalize
            // over this exact request). Drives BundleLayout resolution in
            // PlanStage so FinalizeStage writes manifest.json/reconstruction.json
            // for every encode, not only the coordinator's FinalizeOnly pass.
            // EncodingOptions.EnableMetadataInjection stays unset (defaults
            // false) here, so the emitted ffmpeg command is unaffected. Null
            // when the source has no resolvable movie/episode (e.g. a disc
            // rip) — degrades to today's behavior exactly.
            MediaItem: fileMetadata.MediaItem
        );

        string groupTag = Ulid.NewUlid().ToString();
        (OutputPlan? plan, DecomposedTask[] tasks) = await orchestrator.DecomposeWithPlanAsync(
            request: request,
            groupTag: groupTag
        );

        // A partial top-up rebuilds only the missing renditions; a bundle
        // built from it must not rewrite the master, which already lists
        // the whole set. Cleared if the filter empties and we fall back to
        // a full re-encode below.
        bool isPartialTopUp = false;

        // Partial with a non-empty MissingKinds only ever happens for
        // decomposable (HLS/DASH) strategies — DecideSingleFile never
        // returns Partial with missing kinds, only Skip/Full — so
        // filtering down to the missing kinds here never starves a
        // Whole-task (MKV/MP4) run of its only task.
        if (
            reconciliation is { Action: ReconciliationAction.Partial, MissingKinds.Count: > 0 }
        )
        {
            tasks = tasks.Where(predicate: task => reconciliation.MissingKinds.Contains(value: task.Kind)).ToArray();
            isPartialTopUp = true;

            if (tasks.Length == 0)
            {
                Log.LogWarning(
                    message: "[VideoEncodeJob] Reconciliation flagged {Kinds} as missing for preset '{Name}' but decomposition produced no matching task — falling back to a full re-encode", args: [string.Join(separator: ", ", values: reconciliation.MissingKinds), preset.Name]
                );
                (plan, tasks) = await orchestrator.DecomposeWithPlanAsync(request: request, groupTag: groupTag);
                isPartialTopUp = false;
            }
        }

        bool isWhole = tasks is [{ Kind: EncodeTaskKind.Whole }];

        if (isWhole)
        {
            await RunInlineAsync(
                orchestrator: orchestrator,
                request: request,
                encodingProfile: encodingProfile,
                preset: preset,
                fileMetadata: fileMetadata,
                stopwatch: stopwatch,
                sourceStorage: sourceStorage,
                context: context,
                fileManager: fileManager,
                folder: folder
            );
            return;
        }

        // See the matching guard in RunMergedEncodeAsync: non-Whole tasks
        // only ever come back from a plan that decomposed successfully.
        OutputPlan resolvedPlan =
            plan
            ?? throw new InvalidOperationException(
                message: "DecomposeWithPlanAsync returned decomposed tasks without a Plan — "
                         + "the decode-aware bundler cannot classify outputs without it."
            );

        await DispatchDecomposedAsync(
            tasks: tasks,
            plan: resolvedPlan,
            presetIds: [preset.Id],
            fileMetadata: fileMetadata,
            stopwatch: stopwatch,
            isPartialTopUp: isPartialTopUp
        );
    }

    // ------------------------------------------------------------------
    // Coordinator state machine (subsequent wake-ups)
    // ------------------------------------------------------------------

    private async Task HandleCoordinatorWakeUpAsync()
    {
        CoordinatorState state = Coordinator!;

        // No wake-up trace — the coordinator re-enters this method on every
        // re-enqueue tick (multiple times per second while bundles encode),
        // and even at Verbose it bombards the console. The state-transition
        // logs further down already announce real progress.

        try
        {
            switch (state.Phase)
            {
                case CoordinatorPhase.WaitPass1:
                    await HandleWaitPass1Async(state: state);
                    break;

                case CoordinatorPhase.WaitChildren:
                    await HandleWaitChildrenAsync(state: state);
                    break;

                case CoordinatorPhase.Finalize:
                    await HandleFinalizeAsync(state: state);
                    break;

                default:
                    Log.LogWarning(
                        message: "[VideoEncodeJob] Unknown coordinator phase '{Phase}' — completing job",
                        args: state.Phase
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            // A phase that throws after children have reported progress strands
            // the dashboard card on its last message. Clear it before the
            // exception propagates to the queue's failure handling.
            await EncoderCardTerminator.PublishFailedAsync(
                jobId: Id.ToInt(),
                title: string.Empty,
                inputPath: InputFile,
                message: ex.Message,
                exceptionType: ex.GetType().Name
            );
            throw;
        }
    }

    private async Task HandleWaitPass1Async(CoordinatorState state)
    {
        await using MediaContext context = new();

        string[] pass1TaskIds = state.TaskIds.Where(predicate: tid => tid.Contains(value: "-pass1-")).ToArray();

        List<string> completedTaskIds = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .Where(predicate: o => o.GroupTag == state.GroupTag)
            .Select(selector: o => o.TaskId)
            .ToListAsync();

        bool allPass1Done = pass1TaskIds.All(predicate: tid => completedTaskIds.Contains(item: tid));

        if (!allPass1Done)
        {
            int doneCount = pass1TaskIds.Count(predicate: tid => completedTaskIds.Contains(item: tid));
            Log.LogInformation(
                message: "[VideoEncodeJob] WaitPass1: {DoneCount}/{Length} Pass1 tasks done — re-enqueueing", args: [doneCount, pass1TaskIds.Length]
            );
            ReEnqueueSelf(newState: state with { Phase = CoordinatorPhase.WaitPass1 });
            return;
        }

        // All Pass1 tasks done — resolve stats file path and dispatch Pass2.
        EncodeTaskOutcome? anyPass1Outcome = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .FirstOrDefaultAsync(predicate: o =>
                o.GroupTag == state.GroupTag && o.Kind == nameof(EncodeTaskKind.Pass1)
            );

        string pass1StatsPath =
            anyPass1Outcome?.OutputArtifactsJson?.Split(separator: '\n').FirstOrDefault() ?? string.Empty;

        string[] pass2TaskIds = state.TaskIds.Where(predicate: tid => tid.Contains(value: "-pass2-")).ToArray();

        string[] otherTaskIds = state
            .TaskIds.Where(predicate: tid => !tid.Contains(value: "-pass1-") && !tid.Contains(value: "-pass2-"))
            .ToArray();

        // Dispatch Pass2 children with the resolved stats file path.
        QueueJobDispatcher dispatcher = GetDispatcher();

        foreach (string pass2TaskId in pass2TaskIds)
        {
            int outputIndex = ParseOutputIndex(taskId: pass2TaskId);

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

            EncodeTaskJob childJob = BuildChildJob(
                task: pass2Task,
                presetId: state.PresetId,
                presetIds: state.PresetIds,
                outputDirectory: state.OutputDirectory
            );
            dispatcher.DispatchChild(
                job: childJob,
                onQueue: childJob.QueueName,
                priority: childJob.Priority,
                parentJobId: _selfJobId,
                groupTag: state.GroupTag
            );
        }

        // Dispatch non-pass1/non-pass2 tasks that were held (audio, subtitle, thumbnails).
        foreach (string otherTaskId in otherTaskIds)
        {
            if (completedTaskIds.Contains(item: otherTaskId))
                continue;

            EncodeTaskKind kind = InferKindFromTaskId(taskId: otherTaskId);
            int outputIndex = ParseOutputIndex(taskId: otherTaskId);

            DecomposedTask otherTask = new(
                TaskId: otherTaskId,
                ParentJobId: _selfJobId,
                GroupTag: state.GroupTag,
                Kind: kind,
                OutputIndex: outputIndex,
                Resources: null,
                Label: otherTaskId
            );

            EncodeTaskJob childJob = BuildChildJob(
                task: otherTask,
                presetId: state.PresetId,
                presetIds: state.PresetIds,
                outputDirectory: state.OutputDirectory
            );
            dispatcher.DispatchChild(
                job: childJob,
                onQueue: childJob.QueueName,
                priority: childJob.Priority,
                parentJobId: _selfJobId,
                groupTag: state.GroupTag
            );
        }

        Log.LogTrace(
            message: "[VideoEncodeJob] WaitPass1 complete — dispatched {Length} Pass2 + {Length2} other tasks. Transitioning to WaitChildren.", args: [pass2TaskIds.Length, otherTaskIds.Length]
        );

        ReEnqueueSelf(
            newState: state with
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
            .Where(predicate: o => o.GroupTag == state.GroupTag)
            .Select(selector: o => o.TaskId)
            .ToListAsync();

        // Sequential bundle dispatch: when Bundles[] is set on state, only
        // the CURRENT bundle's BundledTaskIds need to be complete to advance.
        // Each bundle = one ffmpeg invocation; running them one at a time
        // means the host never has two encoder processes fighting for the
        // GPU/CPU at once. The next bundle dispatches on this wake-up.
        if (state.Bundles is { Length: > 0 } bundles && state.CurrentBundleIndex < bundles.Length)
        {
            DecomposedTask currentBundle = bundles[state.CurrentBundleIndex];
            string[] currentBundleTaskIds = currentBundle.BundledTaskIds ?? [currentBundle.TaskId];

            bool currentBundleDone = currentBundleTaskIds.All(predicate: tid =>
                completedTaskIds.Contains(item: tid)
            );

            if (!currentBundleDone)
            {
                int doneCount = currentBundleTaskIds.Count(predicate: tid => completedTaskIds.Contains(item: tid));
                // Polling is fast (sub-second re-enqueue intervals) but child
                // tasks complete on encoder cadence (minutes). Logging on every
                // wake-up produced ~thousands of identical lines per encode —
                // emit only when the count actually advances + at Verbose so
                // routine progress doesn't pollute Info-level dashboards.
                if (doneCount != state.LastLoggedDoneCount)
                {
                    Log.LogTrace(
                        message: "[VideoEncodeJob] WaitChildren: bundle {CurrentBundleIndex}/{Length}, {DoneCount}/{Length2} streams done", args: [state.CurrentBundleIndex + 1, bundles.Length, doneCount, currentBundleTaskIds.Length]
                    );
                }
                ReEnqueueSelf(
                    newState: state with
                    {
                        Phase = CoordinatorPhase.WaitChildren,
                        LastLoggedDoneCount = doneCount,
                    }
                );
                return;
            }

            int nextIndex = state.CurrentBundleIndex + 1;
            if (nextIndex < bundles.Length)
            {
                Log.LogInformation(
                    message: "[VideoEncodeJob] Bundle {CurrentBundleIndex}/{Length} complete. Dispatching bundle {NextIndex}/{Length2}.", args: [state.CurrentBundleIndex + 1, bundles.Length, nextIndex + 1, bundles.Length]
                );
                DispatchSingleBundle(
                    bundle: bundles[nextIndex],
                    presetId: state.PresetId,
                    presetIds: state.PresetIds,
                    groupTag: state.GroupTag,
                    outputDirectory: state.OutputDirectory
                );
                ReEnqueueSelf(
                    newState: state with
                    {
                        Phase = CoordinatorPhase.WaitChildren,
                        CurrentBundleIndex = nextIndex,
                        // Fresh bundle — reset the throttle so the first
                        // progress line for this bundle always emits.
                        LastLoggedDoneCount = -1,
                    }
                );
                return;
            }

            Log.LogInformation(
                message: "[VideoEncodeJob] All {Length} bundles complete. Transitioning to Finalize.",
                args: bundles.Length
            );
            // Finalize is one-shot post-encode work — fire immediately so the
            // library refresh doesn't wait out a full poll interval.
            ReEnqueueSelf(newState: state with { Phase = CoordinatorPhase.Finalize }, availableAfter: TimeSpan.Zero);
            return;
        }

        // Legacy path (no Bundles tracked, e.g. two-pass): wait for every
        // non-pass1 task to land.
        string[] nonPass1TaskIds = state.TaskIds.Where(predicate: tid => !tid.Contains(value: "-pass1-")).ToArray();
        bool allDone = nonPass1TaskIds.All(predicate: tid => completedTaskIds.Contains(item: tid));

        if (!allDone)
        {
            int doneCount = nonPass1TaskIds.Count(predicate: tid => completedTaskIds.Contains(item: tid));
            if (doneCount != state.LastLoggedDoneCount)
            {
                Log.LogTrace(
                    message: "[VideoEncodeJob] WaitChildren: {DoneCount}/{Length} tasks done", args: [doneCount, nonPass1TaskIds.Length]
                );
            }
            ReEnqueueSelf(
                newState: state with
                {
                    Phase = CoordinatorPhase.WaitChildren,
                    LastLoggedDoneCount = doneCount,
                }
            );
            return;
        }

        Log.LogTrace(
            message: "[VideoEncodeJob] WaitChildren complete — all tasks done. Transitioning to Finalize."
        );
        ReEnqueueSelf(newState: state with { Phase = CoordinatorPhase.Finalize }, availableAfter: TimeSpan.Zero);
    }

    private void DispatchSingleBundle(
        DecomposedTask bundle,
        Ulid presetId,
        Ulid[]? presetIds,
        string groupTag,
        string? outputDirectory = null
    )
    {
        DecomposedTask stamped = bundle with { ParentJobId = _selfJobId };
        EncodeTaskJob bundleJob = BuildChildJob(task: stamped, presetId: presetId, presetIds: presetIds, outputDirectory: outputDirectory);
        GetDispatcher()
            .DispatchChild(
                job: bundleJob,
                onQueue: bundleJob.QueueName,
                priority: bundleJob.Priority,
                parentJobId: _selfJobId,
                groupTag: groupTag
            );
    }

    private async Task HandleFinalizeAsync(CoordinatorState state)
    {
        // Open a fresh MediaContext for finalize — nothing is captured from
        // any prior Handle() invocation. This is the key B1 fix.
        await using MediaContext context = new();

        FileRepository fileRepository = new(context: context, storageDriver: StorageDriver);
        FileManager fileManager = new(
            fileRepository: fileRepository,
            storageFactory: StorageFactory,
            storageDriver: StorageDriver,
            mediaAnalyzer: _mediaAnalyzer!
        );

        await using LibraryRepository libraryRepository = new(context: context, storageDriver: StorageDriver);
        Folder? folder = await libraryRepository.GetLibraryFolder(folderId: FolderId);
        if (folder is null)
        {
            Log.LogWarning(
                message: "[VideoEncodeJob] Finalize: folder {FolderId} not found — aborting post-encode",
                args: FolderId
            );
            return;
        }

        FileMetadata fileMetadata = await GetFileMetaData(folder: folder, context: context);
        if (!fileMetadata.Success)
            return;

        List<EncodeTaskOutcome> outcomes = await context
            .EncodeTaskOutcomes.AsNoTracking()
            .Where(predicate: o => o.GroupTag == state.GroupTag)
            .ToListAsync();

        int failedCount = outcomes.Count(predicate: o => !o.Success);

        if (failedCount > 0)
        {
            Log.LogWarning(
                message: "[VideoEncodeJob] Finalize: {FailedCount} task(s) failed — skipping post-encode",
                args: failedCount
            );

            await EncoderCardTerminator.PublishFailedAsync(
                jobId: fileMetadata.Id,
                title: fileMetadata.Title,
                inputPath: InputFile,
                message: $"{failedCount} rung(s) failed",
                exceptionType: "FinalizeChildFailed"
            );

            (IReadOnlyList<string> failedDescriptors, string? lastError) = SummarizeFailures(
                failedOutcomes: outcomes.Where(predicate: o => !o.Success).ToList()
            );

            await new IncompleteEncodeRecorder().RecordAsync(
                context: context,
                mediaId: fileMetadata.Id,
                folderId: FolderId.ToString(),
                title: fileMetadata.Title,
                missingKeys: failedDescriptors,
                lastError: lastError,
                attemptsMade: 0,
                ct: CancellationToken.None
            );

            return;
        }

        IStorage sourceStorage = SourceDriverId.HasValue
            ? StorageFactory.For(folderId: SourceDriverId.Value, driverId: SourceDriverId.Value, subPath: string.Empty)
            : StorageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);

        IStorage destinationStorage = StorageFactory.For(folderId: folder.Id, driverId: folder.DriverId, subPath: folder.Path);

        // Pre-flight check: ensure the shared tempDir exists and contains at least
        // some expected output before proceeding to FinalizeOnly.
        string relativeOutputPath = (fileMetadata.Path ?? string.Empty)
            .Replace(oldChar: '\\', newChar: '/')
            .Trim(trimChar: '/');
        string tempDir = Path.Combine(
            path1: StoragePaths.TranscodeRoot,
            path2: relativeOutputPath.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar)
        );

        // A dispatch-time bundle is a Whole task, and a Whole task is "the only
        // execution": it runs FinalizeStage itself and publishes the tempDir to the
        // destination, which is precisely why the tempDir is empty by the time we
        // get here. Only per-stream slices defer their finalize to this pass, and a
        // run made entirely of bundles has nothing left for it to do.
        //
        // The emptiness check below is meant to catch children that produced
        // nothing. Applying it to a run whose bundles already published turned a
        // success into a failure and skipped everything after it — the subtitle
        // OCR, the library refresh, the completion event — for every bundled
        // encode.
        bool bundlesSelfFinalized =
            state.Bundles is { Length: > 0 } bundles
            && bundles.All(predicate: bundle => bundle.Kind == EncodeTaskKind.Whole);

        if (
            !bundlesSelfFinalized
            && (
                !Directory.Exists(path: tempDir)
                || !Directory.EnumerateFiles(path: tempDir, searchPattern: "*.m3u8", searchOption: SearchOption.AllDirectories).Any()
            )
        )
        {
            Log.LogError(
                message: "[VideoEncodeJob] Finalize: tempDir '{TempDir}' missing or empty. Cannot finalize GroupTag={GroupTag}.", args: [tempDir, state.GroupTag]
            );
            return;
        }

        // Coordinator finalize: run the pipeline once over the shared cache
        // tempDir with Options.FinalizeOnly=true. The orchestrator skips
        // Build + Execute, runs FinalizeStage against the variant playlists
        // the children produced (writing master m3u8, chapters.vtt,
        // fonts.json, manifest.json), then publishes the whole cache to
        // the destination and deletes it. Per-task EncodeAsync runs skip
        // FinalizeStage + publish + cleanup precisely to avoid racing this
        // single coordinator-driven pass.
        IEncodingOrchestrator orchestrator = _encodingOrchestrator!;

        EncodingRequest finalizeRequest;
        await using (MediaContext profileLookup = new())
        {
            if (state.PresetIds is { Length: > 1 } presetIds)
            {
                // Merged run: re-resolve every preset's profile and re-plan +
                // re-merge them (PlanAsync is deterministic given the same
                // profile + source) so FinalizeStage writes a master playlist
                // listing every preset's video rendition instead of
                // re-deriving a plan from just one preset's profile.
                List<EncodingRequest> mergeRequests = new(capacity: presetIds.Length);
                bool resolveFailed = false;

                foreach (Ulid presetId in presetIds)
                {
                    try
                    {
                        EncodingProfile presetProfile = PresetResolver.Resolve(
                            presetId: presetId,
                            lookup: new DbPresetLookup(context: profileLookup)
                        );
                        mergeRequests.Add(
                            item: new(
                                InputPath: InputFile,
                                OutputDirectory: fileMetadata.Path ?? string.Empty,
                                Profile: presetProfile,
                                MediaTitle: fileMetadata.FileName,
                                SourceStorage: sourceStorage,
                                DestinationStorage: destinationStorage,
                                MediaItem: fileMetadata.MediaItem
                            )
                        );
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning(
                            message: "[VideoEncodeJob] Finalize: cannot resolve preset {PresetId} — {Message}", args: [presetId, ex.Message]
                        );
                        resolveFailed = true;
                        break;
                    }
                }

                if (resolveFailed)
                    return;

                OutputPlan? mergedPlan = await orchestrator.PlanMergedAsync(requests: mergeRequests);
                if (mergedPlan is null)
                {
                    Log.LogError(
                        message: "[VideoEncodeJob] Finalize: could not rebuild the merged plan for preset set [{PresetIds}] — aborting post-encode",
                        args: string.Join(separator: ", ", values: presetIds)
                    );
                    return;
                }

                finalizeRequest = mergeRequests[index: 0] with
                {
                    Options = new(FinalizeOnly: true, PrecomputedPlan: mergedPlan),
                };
            }
            else
            {
                EncodingProfile finalizeProfile;
                try
                {
                    finalizeProfile = PresetResolver.Resolve(
                        presetId: state.PresetId,
                        lookup: new DbPresetLookup(context: profileLookup)
                    );
                }
                catch (Exception ex)
                {
                    Log.LogWarning(
                        message: "[VideoEncodeJob] Finalize: cannot resolve preset {PresetId} — {Message}", args: [state.PresetId, ex.Message]
                    );
                    return;
                }

                finalizeRequest = new(
                    InputPath: InputFile,
                    OutputDirectory: fileMetadata.Path ?? string.Empty,
                    Profile: finalizeProfile,
                    Options: new(FinalizeOnly: true),
                    MediaTitle: fileMetadata.FileName,
                    SourceStorage: sourceStorage,
                    DestinationStorage: destinationStorage,
                    // Pure identity — safe here regardless of FinalizeOnly. Drives
                    // BundleLayout resolution + manifest.json/reconstruction.json in
                    // FinalizeStage. EncodingOptions.EnableMetadataInjection stays unset
                    // (defaults false), so this has no effect on the ffmpeg command even
                    // on a request where Build/Execute do run. Null when the source has
                    // no resolvable movie/episode (e.g. a disc rip) — degrades to today's
                    // behavior exactly.
                    MediaItem: fileMetadata.MediaItem
                );
            }
        }

        // The bundles already finalized and published themselves. Running the
        // pipeline again over the tempDir they emptied would only rediscover that
        // there is nothing there. Fall through to post-encode, which is the part
        // that still has work to do. The post-encode scan (FileManager.MakeMetadata)
        // is what rebuilds the master playlist from disk now — see
        // RebuildHlsMasterFromDiskAsync — because it reliably reads the fully-
        // published media root, unlike this coordinator's own scope which can
        // resolve against only the last bundle's own rendition.
        if (bundlesSelfFinalized)
        {
            Log.LogInformation(
                message: "[VideoEncodeJob] Finalize: bundles for GroupTag={GroupTag} finalized and published themselves; continuing to post-encode.",
                args: state.GroupTag
            );
        }
        else
        {
            await PublishStageAsync(fileMetadata: fileMetadata, message: "Publishing artifacts");
            try
            {
                EncodingResult publishResult = await orchestrator.EncodeAsync(
                    request: finalizeRequest,
                    ct: _shutdownToken
                );
                if (!publishResult.Success)
                {
                    string err =
                        publishResult.Error?.Message
                        ?? publishResult.EnrichedError?.Message
                        ?? "finalize-only pass failed with no details";
                    Log.LogError(
                        message: "[VideoEncodeJob] Coordinator finalize failed for GroupTag={GroupTag}: {Err}", args: [state.GroupTag, err]
                    );

                    await new IncompleteEncodeRecorder().RecordAsync(
                        context: context,
                        mediaId: fileMetadata.Id,
                        folderId: FolderId.ToString(),
                        title: fileMetadata.Title,
                        missingKeys: ["finalize"],
                        lastError: err,
                        attemptsMade: 0,
                        ct: CancellationToken.None
                    );

                    await EncoderCardTerminator.PublishFailedAsync(
                        jobId: fileMetadata.Id,
                        title: fileMetadata.Title,
                        inputPath: InputFile,
                        message: err,
                        exceptionType: "FinalizeFailed"
                    );

                    return;
                }
            }
            catch (Exception ex)
            {
                Log.LogError(
                    message: "[VideoEncodeJob] Coordinator finalize threw for GroupTag={GroupTag}: {Message}", args: [state.GroupTag, ex.Message]
                );
                throw;
            }
        }

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Checking source subtitles");
        await RunBitmapSubtitleOcrAsync(fileMetadata: fileMetadata, inputPath: InputFile, sourceStorage: sourceStorage, destinationStorage: destinationStorage);

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Refreshing library");
        Library library = folder.FolderLibraries.First().Library;
        await ScanEncodedOutputWithRetryAsync(
            fileManager: fileManager,
            mediaId: fileMetadata.Id,
            title: fileMetadata.Title,
            library: library,
            filterFileName: DeriveScanFilter(outputPath: fileMetadata.Path, fallbackFileName: fileMetadata.FileName)
        );

        await new IncompleteEncodeRecorder().ClearAsync(
            context: context,
            mediaId: fileMetadata.Id,
            folderId: FolderId.ToString(),
            ct: CancellationToken.None
        );

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingCompletedEvent
                {
                    JobId = fileMetadata.Id,
                    OutputPath = fileMetadata.Path ?? string.Empty,
                    Duration = TimeSpan.Zero,
                }
            );
        }

        Log.LogInformation(
            message: "[VideoEncodeJob] Finalize complete for GroupTag={GroupTag}",
            args: state.GroupTag
        );
    }

    // ------------------------------------------------------------------
    // Post-encode registration
    // ------------------------------------------------------------------

    private static readonly TimeSpan[] PostEncodeScanRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(seconds: 5),
        TimeSpan.FromSeconds(seconds: 10),
        TimeSpan.FromSeconds(seconds: 20),
        TimeSpan.FromSeconds(seconds: 25),
    ];

    /// <summary>
    /// Registers the just-published encode output, retrying the filtered scan on a
    /// bounded backoff when it resolves 0 files. Remote-storage directory listings
    /// (NFS <c>acdirmax</c>, S3 read-after-write) can hide a just-created entry for
    /// tens of seconds right after publish, so an immediate scan can come back empty
    /// even though the output was written successfully — the delayed manual rescan a
    /// user runs afterward then "just works" because enough time has passed. The
    /// retried scan is the same filtered, additive call — <see cref="FileManager.FindFiles"/>
    /// never deletes existing records while a <see cref="FileManager.FilterFiles"/> filter
    /// is set — so re-running it here is safe.
    /// </summary>
    private async Task ScanEncodedOutputWithRetryAsync(
        FileManager fileManager,
        int mediaId,
        string title,
        Library library,
        string filterFileName
    )
    {
        for (int attempt = 0; attempt < PostEncodeScanRetryDelays.Length; attempt++)
        {
            TimeSpan delay = PostEncodeScanRetryDelays[attempt];
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay: delay, cancellationToken: _shutdownToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            fileManager.FilterFiles(filter: filterFileName);
            bool hasCandidates = await fileManager.FindFiles(id: mediaId, library: library);
            if (hasCandidates)
                return;
        }

        Log.LogWarning(
            message: "[VideoEncodeJob] Post-encode registration found 0 files for id={Id} '{Title}' (filter='{Filter}') after {Attempts} attempts — a manual rescan will be required; check storage visibility/naming", args: [mediaId, title, filterFileName, PostEncodeScanRetryDelays.Length]
        );
    }

    // ------------------------------------------------------------------
    // Quarantine helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds the quarantine descriptor list and first error from a set of
    /// failed <see cref="EncodeTaskOutcome"/> rows. Pure — no I/O.
    /// </summary>
    /// <summary>
    /// Chooses the filter the post-encode registration scan runs with. Anchors on
    /// the leaf of the output directory the encoder just wrote to
    /// (<paramref name="outputPath"/>) rather than the reconstructed file name.
    /// <paramref name="fallbackFileName"/> is <c>CreateFileName()</c>
    /// (<c>show.SxxExx.episodeTitle.NoMercy</c>); its episode-title segment is
    /// re-cleaned at scan time and drifts from the name written at encode time
    /// (apostrophes, unicode, a changed cleaning rule), so a
    /// <c>file.Contains(filter)</c> match against it silently registered nothing
    /// and left users forcing an unfiltered rescan. The directory leaf carries the
    /// stable <c>show.SxxExx</c> (or <c>movie.(year)</c>) token every output file
    /// lives under, and is exactly the folder this run wrote into. Falls back to
    /// the file name only when the output path is empty.
    /// </summary>
    internal static string DeriveScanFilter(string? outputPath, string fallbackFileName)
    {
        string leaf =
            outputPath
                ?.Trim(trimChars: ['/', '\\'])
                .Split(separator: ['/', '\\'], options: StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()
            ?? string.Empty;

        return string.IsNullOrWhiteSpace(value: leaf) ? fallbackFileName : leaf;
    }

    internal static (IReadOnlyList<string> descriptors, string? lastError) SummarizeFailures(
        IReadOnlyList<EncodeTaskOutcome> failedOutcomes
    )
    {
        Dictionary<string, int> kindCounts = [];

        foreach (EncodeTaskOutcome outcome in failedOutcomes)
        {
            kindCounts.TryGetValue(key: outcome.Kind, value: out int count);
            kindCounts[key: outcome.Kind] = count + 1;
        }

        List<string> descriptors = kindCounts
            .Select(selector: pair => pair.Value > 1 ? $"{pair.Key} ({pair.Value}x)" : pair.Key)
            .ToList();

        string? lastError = failedOutcomes
            .Select(selector: o => o.ErrorMessage)
            .FirstOrDefault(predicate: msg => !string.IsNullOrWhiteSpace(value: msg));

        return (descriptors, lastError ?? "one or more rungs failed");
    }

    // ------------------------------------------------------------------
    // Dispatch helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Dispatches all decomposed children and re-enqueues the coordinator with
    /// phase state so it can poll for completion after a restart.
    /// </summary>
    /// <summary>
    /// Dispatches all decomposed children for the run. <paramref name="presetIds"/>
    /// is every preset id the decomposed <paramref name="tasks"/> cover — a
    /// single-element array for the legacy per-preset path, or every preset
    /// unioned into a smart-orchestrator merged run. Index 0 is always the
    /// primary preset (<see cref="CoordinatorState.PresetId"/>); the full
    /// array is only carried on <see cref="CoordinatorState.PresetIds"/> when
    /// there is more than one, so every existing single-preset serialized job
    /// keeps deserializing to exactly the shape it did before this array
    /// existed.
    /// </summary>
    private async Task DispatchDecomposedAsync(
        DecomposedTask[] tasks,
        OutputPlan plan,
        Ulid[] presetIds,
        FileMetadata fileMetadata,
        Stopwatch stopwatch,
        bool isPartialTopUp = false
    )
    {
        int parentJobId = _selfJobId;
        string groupTag = tasks[0].GroupTag;
        Ulid primaryPresetId = presetIds[0];
        Ulid[]? mergedPresetIds = presetIds.Length > 1 ? presetIds : null;

        Log.LogTrace(
            message: "[VideoEncodeJob] Decomposed into {Length} child tasks (groupTag={GroupTag})", args: [tasks.Length, groupTag]
        );

        QueueJobDispatcher dispatcher = GetDispatcher();

        bool hasTwoPass = tasks.Any(predicate: task =>
            task.Kind == EncodeTaskKind.Pass1 || task.Kind == EncodeTaskKind.Pass2
        );

        string[] allTaskIds = tasks.Select(selector: task => task.TaskId).ToArray();

        if (hasTwoPass)
        {
            // Two-pass: dispatch only Pass1 tasks first. Pass2 and aux are dispatched
            // by the coordinator after all Pass1 tasks complete.
            DecomposedTask[] pass1Tasks = tasks
                .Where(predicate: task => task.Kind == EncodeTaskKind.Pass1)
                .ToArray();

            foreach (DecomposedTask task in pass1Tasks)
            {
                DecomposedTask stamped = task with { ParentJobId = parentJobId };
                EncodeTaskJob childJob = BuildChildJob(
                    task: stamped,
                    presetId: primaryPresetId,
                    presetIds: mergedPresetIds,
                    outputDirectory: fileMetadata.Path
                );
                dispatcher.DispatchChild(
                    job: childJob,
                    onQueue: childJob.QueueName,
                    priority: childJob.Priority,
                    parentJobId: parentJobId,
                    groupTag: groupTag
                );
            }

            Log.LogInformation(
                message: "[VideoEncodeJob] Dispatched {Length} Pass1 tasks. Transitioning to WaitPass1.",
                args: pass1Tasks.Length
            );

            ReEnqueueSelf(
                newState: new(
                    GroupTag: groupTag,
                    TaskIds: allTaskIds,
                    Phase: CoordinatorPhase.WaitPass1,
                    Pass1DispatchedAt: DateTime.UtcNow,
                    Pass2DispatchedAt: null,
                    Pass1StatsPath: null,
                    PresetId: primaryPresetId,
                    ExpectedFinalCount: tasks.Count(predicate: task => task.Kind != EncodeTaskKind.Pass1),
                    OutputDirectory: fileMetadata.Path,
                    PresetIds: mergedPresetIds
                )
            );
        }
        else
        {
            // Single-pass: pack tasks into resource-proportional bundles and
            // dispatch ONE bundle at a time. The coordinator state carries
            // the full bundle list and an index; WaitChildren waits for the
            // current bundle's BundledTaskIds, then dispatches the next on
            // wake-up. One ffmpeg in flight per source — never N parallel
            // bundles racing the GPU / CPU / shared writes.
            DecomposedTask[] bundles = BuildResourceBundles(
                tasks: tasks,
                plan: plan,
                parentJobId: parentJobId,
                groupTag: groupTag,
                isPartialTopUp: isPartialTopUp
            );

            if (bundles.Length == 0)
            {
                Log.LogInformation(message: "[VideoEncodeJob] No bundles produced — nothing to dispatch.");
                return;
            }

            EncodeTaskJob firstBundleJob = BuildChildJob(
                task: bundles[0],
                presetId: primaryPresetId,
                presetIds: mergedPresetIds,
                outputDirectory: fileMetadata.Path
            );
            dispatcher.DispatchChild(
                job: firstBundleJob,
                onQueue: firstBundleJob.QueueName,
                priority: firstBundleJob.Priority,
                parentJobId: parentJobId,
                groupTag: groupTag
            );

            Log.LogInformation(
                message: "[VideoEncodeJob] Dispatched bundle 1/{Length} covering {Length2} streams. Sequential dispatch — bundle N+1 fires on bundle N completion. Transitioning to WaitChildren.", args: [bundles.Length, tasks.Length]
            );

            ReEnqueueSelf(
                newState: new(
                    GroupTag: groupTag,
                    TaskIds: allTaskIds,
                    Phase: CoordinatorPhase.WaitChildren,
                    Pass1DispatchedAt: null,
                    Pass2DispatchedAt: null,
                    Pass1StatsPath: null,
                    PresetId: primaryPresetId,
                    ExpectedFinalCount: tasks.Length,
                    Bundles: bundles,
                    CurrentBundleIndex: 0,
                    OutputDirectory: fileMetadata.Path,
                    PresetIds: mergedPresetIds
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
    /// <summary>
    /// How long a polling coordinator (WaitPass1 / WaitChildren) sleeps
    /// before its next wake-up. Children run on encoder cadence (minutes),
    /// so a 5s poll interval just stamped the queue with thousands of
    /// no-op DB hits per minute. 30s is well below typical encode lengths
    /// while cutting wake-up rate ~6x.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(seconds: 30);

    /// <summary>
    /// ±5s jitter prevents N concurrent coordinators from waking in
    /// lockstep — a season with 12 episodes used to burst-poll all 12
    /// in ~100ms every 5s. Jitter spreads them across the poll window.
    /// </summary>
    private static readonly TimeSpan PollJitterRange = TimeSpan.FromSeconds(seconds: 5);

    private static readonly Random PollJitterRng = new();

    private static TimeSpan NextPollDelay()
    {
        double offsetSeconds =
            (PollJitterRng.NextDouble() * 2.0 - 1.0) * PollJitterRange.TotalSeconds;
        return PollInterval + TimeSpan.FromSeconds(value: offsetSeconds);
    }

    private void ReEnqueueSelf(CoordinatorState newState, TimeSpan? availableAfter = null)
    {
        // Bump WakeSequence so the serialized payload differs from the row this
        // worker is currently processing. JobQueue.Enqueue dedups by Payload, and
        // ReEnqueueSelf is invoked from inside Handle BEFORE the worker calls
        // DeleteJob on the original — without this nonce, identical-state
        // wake-ups (e.g. WaitChildren polling the same bundle) collide with the
        // still-reserved original and get silently dropped, killing the
        // coordinator after one tick.
        CoordinatorState bumped = newState with
        {
            WakeSequence = newState.WakeSequence + 1,
        };

        VideoEncodeJob continueJob = new()
        {
            Id = Id,
            FolderId = FolderId,
            LibraryId = LibraryId,
            InputFile = InputFile,
            SourceDriverId = SourceDriverId,
            Status = "running",
            Coordinator = bumped,
        };

        QueueJobDispatcher dispatcher = GetDispatcher();
        JobQueue queue =
            QueueRunner.Current?.Queue
            ?? throw new InvalidOperationException(
                message: "QueueRunner.Current.Queue is null — queue not initialized"
            );

        // Dispatch as a new top-level coordinator job (no parent ID).
        // The different Coordinator payload means deduplication won't block it.
        TimeSpan delay = availableAfter ?? NextPollDelay();
        queue.Enqueue(
            queueJob: new()
            {
                Queue = QueueName,
                Payload = SerializationHelper.Serialize(obj: continueJob),
                Priority = Priority,
                AvailableAt = DateTime.UtcNow + delay,
            }
        );

        // No re-enqueue trace — fires every coordinator tick (sub-second cadence
        // while bundles encode) and even at Verbose it floods the console. The
        // companion wake-up trace was already dropped for the same reason; real
        // phase transitions emit their own descriptive lines.
    }

    private EncodeTaskJob BuildChildJob(DecomposedTask task, Ulid presetId) =>
        BuildChildJob(task: task, presetId: presetId, presetIds: null, outputDirectory: null);

    private EncodeTaskJob BuildChildJob(
        DecomposedTask task,
        Ulid presetId,
        string? outputDirectory
    ) => BuildChildJob(task: task, presetId: presetId, presetIds: null, outputDirectory: outputDirectory);

    private EncodeTaskJob BuildChildJob(
        DecomposedTask task,
        Ulid presetId,
        Ulid[]? presetIds,
        string? outputDirectory
    )
    {
        return new()
        {
            LibraryId = LibraryId,
            FolderId = FolderId,
            Id = Id,
            InputFile = InputFile,
            SourceDriverId = SourceDriverId,
            PresetId = presetId,
            PresetIds = presetIds,
            Task = task,
            OutputDirectory = outputDirectory,
        };
    }

    private static QueueJobDispatcher GetDispatcher()
    {
        return QueueRunner.Current?.Dispatcher
            ?? throw new InvalidOperationException(
                message: "QueueRunner.Current is null — queue not initialized"
            );
    }

    /// <summary>
    /// Pack ready single-pass tasks into decode-cost-driven bundles. Thin
    /// wrapper: resolves this host's GPU/CPU stream caps (Layer 2 capacity
    /// scheduling input, unchanged from before this refactor) then hands
    /// the actual grouping decision to <see cref="DecodeAwareBundlePlanner"/>
    /// — a plan-aware, unit-testable component that classifies every output
    /// by decode cost before applying host capacity.
    ///
    /// <para>A full ffmpeg decode is the real cost, not a rung count: a
    /// stream-copied output needs no decode at all, every plain transcode
    /// rung off the same source shares ONE decode via the filtergraph
    /// split the builder already emits, and every HDR→SDR tonemap rung
    /// shares its own single tonemap pass (and the thumbnail sprite) the
    /// same way. See <see cref="DecodeAwareBundlePlanner"/> for the full
    /// two-layer model.</para>
    /// </summary>
    private DecomposedTask[] BuildResourceBundles(
        DecomposedTask[] tasks,
        OutputPlan plan,
        int parentJobId,
        string groupTag,
        bool isPartialTopUp = false
    )
    {
        (int gpuCap, int cpuCap) = ResolveHostCaps(tasks: tasks);

        return DecodeAwareBundlePlanner.Plan(
            tasks: tasks,
            plan: plan,
            parentJobId: parentJobId,
            groupTag: groupTag,
            gpuCap: gpuCap,
            cpuCap: cpuCap,
            isPartialTopUp: isPartialTopUp
        );
    }

    /// <summary>
    /// Resolve per-host bundle caps from <see cref="BundleCapResolver"/>.
    /// Caps are derived from real per-rung benchmark measurements
    /// (<see cref="IHardwareBenchmark"/> → <see cref="SpeedIndex"/>) on this
    /// exact host — no hardcoded model-name tiers, no driver-allowed
    /// maximums conflated with practical throughput. A weak GPU with a
    /// slow benchmark earns a smaller cap; a strong GPU earns a larger
    /// one. Driver-imposed session limits still apply as an outer ceiling.
    /// </summary>
    private (int GpuCap, int CpuCap) ResolveHostCaps(DecomposedTask[] tasks)
    {
        IHardwareBenchmark? benchmark = _hardwareBenchmark;
        IHardwareCapabilities? hardware = _hardwareCapabilities;

        BundleCapResolver.PlannedRung[] plannedRungs = tasks
            .Where(predicate: task =>
                task.Kind == EncodeTaskKind.Video
                && !string.IsNullOrEmpty(value: task.VideoEncoderName)
                && task.VideoWidth > 0
            )
            .Select(selector: task => new BundleCapResolver.PlannedRung(
                Codec: InferCodec(encoderName: task.VideoEncoderName!),
                EncoderName: task.VideoEncoderName!,
                Width: task.VideoWidth,
                IsGpu: task.Resources?.GpuDeviceKey is not null
            ))
            .ToArray();

        return BundleCapResolver.Resolve(rungs: plannedRungs, benchmark: benchmark, hardware: hardware);
    }

    /// <summary>
    /// Derive the video codec family from an ffmpeg encoder name. Matches
    /// the convention used by <see cref="SpeedIndex"/> keys so a benchmark
    /// lookup hits the same row written by the calibration run.
    /// </summary>
    private static VideoCodecType InferCodec(string encoderName)
    {
        string lower = encoderName.ToLowerInvariant();
        if (lower.Contains(value: "av1"))
            return VideoCodecType.Av1;
        if (lower.Contains(value: "265") || lower.Contains(value: "hevc"))
            return VideoCodecType.H265;
        if (lower.Contains(value: "vp9"))
            return VideoCodecType.Vp9;
        return VideoCodecType.H264;
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
        IEncoderProcessRegistry? processRegistry = _encoderProcessRegistry;

        EventBusProgressObserver progressObserver = new(
            jobId: fileMetadata.Id,
            title: fileMetadata.Title,
            baseFolder: fileMetadata.Path,
            sharePath: fileMetadata.Path,
            videoStreams: SummarizeVideo(profile: encodingProfile),
            audioStreams: encodingProfile
                .Audio.Select(selector: audio =>
                    $"{audio.Codec.ToString().ToLowerInvariant()} {audio.Channels}ch"
                )
                .ToList(),
            subtitleStreams: encodingProfile
                .Subtitles.Select(selector: subtitle => subtitle.Codec.ToString().ToLowerInvariant())
                .ToList(),
            hasGpu: false,
            isHdr: false,
            registry: processRegistry
        );

        EncodingResult result = await orchestrator.EncodeAsync(
            request: request,
            progress: progressObserver,
            ct: _shutdownToken
        );

        if (!result.Success)
        {
            throw new InvalidOperationException(
                message: $"Encoding failed for {InputFile}: {result.Error?.Message ?? "unknown error"}"
            );
        }

        Log.LogInformation(
            message: "Encoded {InputFile} → {OutputPath} in {TotalSeconds:F1}s ({Unknown})", args: [InputFile, result.OutputPath, result.Duration.TotalSeconds, result.Metrics?.EncoderUsed ?? "unknown"]
        );

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Recording encoding history");
        await RecordEncodingHistoryAsync(context: context, preset: preset, result: result, inputPath: InputFile, storageDriver: StorageDriver);

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Checking source subtitles");
        await RunBitmapSubtitleOcrAsync(
            fileMetadata: fileMetadata,
            inputPath: InputFile,
            sourceStorage: sourceStorage,
            destinationStorage: request.DestinationStorage ?? request.SourceStorage ?? sourceStorage
        );

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Refreshing library");
        await ScanEncodedOutputWithRetryAsync(
            fileManager: fileManager,
            mediaId: fileMetadata.Id,
            title: fileMetadata.Title,
            library: folder.FolderLibraries.First().Library,
            filterFileName: fileMetadata.FileName
        );

        if (EventBusProvider.IsConfigured)
        {
            stopwatch.Stop();
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingCompletedEvent
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
                    item: $"{rung.Width}x{rung.Height} {rung.Codec.ToString().ToLowerInvariant()}"
                );
            return summary;
        }

        if (profile.Video is { } video)
            summary.Add(
                item: $"{video.Width}x{video.Height ?? 0} {video.Codec.ToString().ToLowerInvariant()}"
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
                if (storageDriver.FileExists(path: inputPath))
                    inputSize = storageDriver.GetFileSize(path: inputPath);
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
                entity: new()
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
                message: $"Could not write encoding history: {ex.Message}",
                level: LogEventLevel.Warning
            );
        }
    }

    // ------------------------------------------------------------------
    // Reconciliation — decide, before any ffmpeg command is built, what a
    // re-dispatch of an already-encoded file actually still needs to do.
    // ------------------------------------------------------------------

    /// <summary>
    /// Gathers what already exists for this (media, preset) combination and
    /// asks <see cref="IEncodeReconciler"/> what to do about it.
    /// <see cref="ForceFullReencode"/> short-circuits straight to Full
    /// without inspecting the destination at all — the operator escape
    /// hatch never pays the directory-listing / analysis cost either.
    /// </summary>
    private async Task<ReconciliationDecision> ReconcileAsync(
        EncodingProfile encodingProfile,
        FileMetadata fileMetadata,
        IStorage sourceStorage,
        IStorage destinationStorage
    )
    {
        IEncodeReconciler reconciler = _encodeReconciler!;

        if (ForceFullReencode)
            return reconciler.Decide(
                input: new(
                    Profile: encodingProfile,
                    IsSingleFileOutput: IsSingleFileContainer(container: encodingProfile.Container),
                    BitmapSubtitleStreamCount: 0,
                    Existing: ExistingOutputSnapshot.Empty,
                    Force: true
                )
            );

        SourceReconciliationFacts source = await ProbeSourceForReconciliationAsync(sourceStorage: sourceStorage);

        ExistingOutputSnapshot existing = await reconciler.InspectAsync(
            mediaRootPath: fileMetadata.Path,
            presetId: encodingProfile.Id.ToString(),
            destinationStorage: destinationStorage,
            ct: CancellationToken.None
        );

        return reconciler.Decide(
            input: new(
                Profile: encodingProfile,
                IsSingleFileOutput: IsSingleFileContainer(container: encodingProfile.Container),
                BitmapSubtitleStreamCount: source.BitmapSubtitleStreamCount,
                Existing: existing,
                SourceChapterCount: source.ChapterCount
            )
        );
    }

    /// <summary>
    /// What reconciliation needs to know about the SOURCE: how many bitmap
    /// (PGS/VobSub/DVB) subtitle streams <see cref="RunBitmapSubtitleOcrAsync"/>
    /// would OCR, and whether there are chapters for FinalizeStage to write.
    /// Both come from one analyzer call; the ffprobe pass is negligible next to
    /// the encode it lets reconciliation skip.
    /// </summary>
    private readonly record struct SourceReconciliationFacts(
        int BitmapSubtitleStreamCount,
        int ChapterCount
    );

    private async Task<SourceReconciliationFacts> ProbeSourceForReconciliationAsync(
        IStorage sourceStorage
    )
    {
        IMediaAnalyzer? analyzer = _mediaAnalyzer;
        if (analyzer is null)
            return new(BitmapSubtitleStreamCount: 0, ChapterCount: 0);

        try
        {
            MediaInfo mediaInfo = await analyzer.AnalyzeAsync(
                filePath: InputFile,
                sourceStorage: sourceStorage,
                ct: CancellationToken.None
            );
            return new(
                BitmapSubtitleStreamCount: mediaInfo.SubtitleStreams.Count(predicate: subtitle => !subtitle.IsTextBased),
                ChapterCount: mediaInfo.Chapters.Count
            );
        }
        catch (Exception ex)
        {
            // Assuming none leaves reconciliation expecting nothing extra, which
            // degrades to "the output as it stands is complete" rather than to a
            // re-encode we cannot justify.
            Log.LogWarning(
                message: "Could not analyze {InputFile} for reconciliation — assuming no bitmap subtitles and no chapters: {Message}", args: [InputFile, ex.Message]
            );
            return new(BitmapSubtitleStreamCount: 0, ChapterCount: 0);
        }
    }

    /// <summary>
    /// Single-file containers (MKV/MP4/audio-only) never decompose past a
    /// single Whole task — reconciliation for those can only ever be Skip or
    /// Full, never Partial-with-missing-kinds.
    /// </summary>
    private static bool IsSingleFileContainer(Container container) =>
        container
            is Container.Mkv
                or Container.Mp4
                or Container.Mp3
                or Container.Aac
                or Container.Flac
                or Container.Ogg
                or Container.Mka
                or Container.Mks;

    /// <summary>
    /// Runs when reconciliation finds every real output already valid and
    /// on-profile, with only the bitmap-subtitle OCR sidecar missing — the
    /// Frieren regression this whole reconciler exists to fix. No ffmpeg
    /// Build/Execute pass runs; only the lightweight OCR pass plus the
    /// post-encode library rescan.
    /// </summary>
    private async Task RunOcrTopUpAsync(
        FileMetadata fileMetadata,
        IStorage sourceStorage,
        IStorage destinationStorage,
        FileManager fileManager,
        Folder folder
    )
    {
        await PublishStageAsync(fileMetadata: fileMetadata, message: "Converting subtitles");
        await RunBitmapSubtitleOcrAsync(fileMetadata: fileMetadata, inputPath: InputFile, sourceStorage: sourceStorage, destinationStorage: destinationStorage);

        await PublishStageAsync(fileMetadata: fileMetadata, message: "Refreshing library");
        await ScanEncodedOutputWithRetryAsync(
            fileManager: fileManager,
            mediaId: fileMetadata.Id,
            title: fileMetadata.Title,
            library: folder.FolderLibraries.First().Library,
            filterFileName: fileMetadata.FileName
        );

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingCompletedEvent
                {
                    JobId = fileMetadata.Id,
                    OutputPath = fileMetadata.Path ?? string.Empty,
                    Duration = TimeSpan.Zero,
                }
            );
        }
    }

    private async Task RunBitmapSubtitleOcrAsync(
        FileMetadata fileMetadata,
        string inputPath,
        IStorage sourceStorage,
        IStorage destinationStorage
    )
    {
        IMediaAnalyzer? analyzer = _mediaAnalyzer;
        ISubtitleOcrEngine? ocrEngine = _subtitleOcrEngine;

        if (analyzer is null || ocrEngine is null)
            return;

        MediaInfo mediaInfo;
        try
        {
            mediaInfo = await analyzer.AnalyzeAsync(
                filePath: inputPath,
                sourceStorage: sourceStorage,
                ct: CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                message: "Could not analyze {InputPath} for OCR: {Message}", args: [inputPath, ex.Message]
            );
            return;
        }

        IReadOnlyList<BitmapSubtitleRef> bitmap = BitmapSubtitleSelector.Select(
            subtitleStreams: mediaInfo.SubtitleStreams
        );

        if (bitmap.Count == 0)
            return;

        // The encode's own output directory, addressed against the destination
        // storage rather than resolved to a local path: a remote destination
        // (NFS/S3) has none, and falling back to "next to the source" wrote the
        // sidecar to a driver-relative path that landed under the server's
        // working directory instead of the library.
        string ocrOutputDirectory = fileMetadata.Path;

        // Classified across every subtitle stream at once, never per stream: the
        // variant depends on a track's peers in the same language, and the
        // sidecar has to carry the same one as the .mks the extraction pass wrote
        // or the two never pair up.
        IReadOnlyList<string> variants = SubtitleClassifier.ResolveVariants(
            streams: mediaInfo.SubtitleStreams
        );

        if (EventBusProvider.IsConfigured)
        {
            await EventBusProvider.Current.PublishAsync(
                @event: new EncodingStageChangedEvent
                {
                    JobId = fileMetadata.Id,
                    Status = "running",
                    Title = fileMetadata.Title,
                    Message = "Converting subtitles",
                }
            );
        }

        foreach ((int subtitleIndex, SubtitleStreamInfo stream) in bitmap)
        {
            string language = stream.Language ?? "eng";

            try
            {
                SubtitleTrack track = await ocrEngine.OcrAsync(
                    inputPath: inputPath,
                    streamIndex: subtitleIndex,
                    language: language,
                    outputFormat: SubtitleCodecType.WebVtt,
                    ct: CancellationToken.None,
                    sourceStorage: sourceStorage,
                    sidecar: new OcrSidecarTarget(
                        Storage: destinationStorage,
                        OutputDirectory: ocrOutputDirectory,
                        MediaTitle: fileMetadata.FileName,
                        Variant: variants[index: subtitleIndex]
                    )
                );
                Log.LogInformation(
                    message: "OCR {Language} → {FilePath} ({CueCount} cues)", args: [language, track.FilePath, track.CueCount]
                );
            }
            catch (Exception ex)
            {
                // Best-effort: OCR is a nice-to-have sidecar, never allowed to
                // fail an already-completed encode. ex.Message already carries
                // the real ffmpeg stderr tail (SubtitleOcrEngine embeds it), so
                // this warning stays actionable instead of a bare "OCR failed".
                Log.LogWarning(
                    message: "OCR failed for {InputPath} subtitle {SubtitleIndex} / abs stream {Index} ({Language}): {Message}", args: [inputPath, subtitleIndex, stream.Index, language, ex.Message]
                );
            }
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
        string? imgPath = movie?.Backdrop ?? episode?.Still;
        MediaItemRef mediaItem = MediaItemRefFactory.Create(movie: movie, episode: episode);

        return new()
        {
            Success = true,
            FolderName = folderName,
            Title = title,
            FileName = fileName,
            Path = basePath,
            Id = baseId,
            ImgPath = imgPath,
            MediaItem = mediaItem,
        };
    }

    /// <summary>
    /// Builds the reconstruction-metadata reference for the movie or episode
    /// being encoded. <paramref name="movie"/> and <paramref name="episode"/>
    /// are mutually exclusive — exactly one is non-null (callers already
    /// verified that before reaching this point).
    /// </summary>
    // ------------------------------------------------------------------
    // Task-ID parsing helpers
    // ------------------------------------------------------------------

    private static int ParseOutputIndex(string taskId)
    {
        int dashIndex = taskId.LastIndexOf(value: '-');
        if (dashIndex >= 0 && int.TryParse(s: taskId.AsSpan(start: dashIndex + 1), result: out int index))
            return index;
        return 0;
    }

    private static EncodeTaskKind InferKindFromTaskId(string taskId)
    {
        if (taskId.Contains(value: "-audio-"))
            return EncodeTaskKind.Audio;
        if (taskId.Contains(value: "-sub-"))
            return EncodeTaskKind.Subtitle;
        if (taskId.Contains(value: "-thumbs"))
            return EncodeTaskKind.Thumbnails;
        if (taskId.Contains(value: "-video-"))
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

        /// <summary>
        /// The resolved movie/episode reference for reconstruction-metadata
        /// wiring. Set on every <see cref="EncodingRequest"/> this job builds —
        /// the inline Whole-task request in <see cref="HandleInitialRunAsync"/>
        /// and the coordinator's FinalizeOnly request in
        /// <see cref="HandleFinalizeAsync"/> alike. Pure identity: it can never
        /// change the ffmpeg command that produced the output, because that is
        /// gated separately by <see cref="EncodingOptions.EnableMetadataInjection"/>,
        /// which this job never sets.
        /// </summary>
        public MediaItemRef? MediaItem { get; set; }
    }

    private static async Task PublishStageAsync(FileMetadata fileMetadata, string message)
    {
        if (!EventBusProvider.IsConfigured)
            return;
        await EventBusProvider.Current.PublishAsync(
            @event: new EncodingStageChangedEvent
            {
                JobId = fileMetadata.Id,
                Status = "running",
                Title = fileMetadata.Title,
                Message = message,
            }
        );
    }
}
