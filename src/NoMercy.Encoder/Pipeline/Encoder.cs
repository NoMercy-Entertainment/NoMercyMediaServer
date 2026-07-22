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
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Pipeline;

public class Encoder(
    IAnalysisStage analyzeStage,
    IValidationStage validateStage,
    IPlanStage planStage,
    IBuildStage buildStage,
    IExecutionStage executeStage,
    IFinalizeStage finalizeStage,
    ILogger<Encoder> logger,
    IProfileOverride? profileOverride = null
) : IEncoder
{
    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    )
    {
        EncodingContext context = EncodingContext.Create() with
        {
            SourceStorage = request.SourceStorage,
            // When caller only sets SourceStorage, treat destination as the same.
            // Explicit DestinationStorage wins when source ≠ destination folders.
            DestinationStorage = request.DestinationStorage ?? request.SourceStorage,
            OutputDirectory = request.OutputDirectory,
            InputPath = request.InputPath,
            // Pure identity — safe to thread through on every request. See
            // EncodingRequest.MediaItem / EncodingOptions.EnableMetadataInjection.
            MediaItem = request.MediaItem,
            OriginalInputPath = request.OriginalInputPath,
            OriginalOutputDirectory = request.OriginalOutputDirectory,
            EnableMetadataInjection = request.Options?.EnableMetadataInjection ?? false,
        };
        Stopwatch stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            message: "[{CorrelationId}] Starting encode: {Input} → {Output}", args: [context.CorrelationId, request.InputPath, request.OutputDirectory]
        );

        // Stage 1: Analyze
        progress?.OnStageStarted(stageName: "Analyze");
        StageResult analyzeResult = await analyzeStage.ExecuteAsync(input: request.InputPath, context: context, ct: ct);
        if (analyzeResult is StageFailure analyzeFailure)
            return Fail(error: analyzeFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };
        progress?.OnStageCompleted(stageName: "Analyze", duration: stopwatch.Elapsed);

        // Profile override seam: a plugin may replace the configured profile based
        // on the analyzed source (e.g. detect anime → an anime preset). Applied
        // once here so every downstream stage sees the effective profile.
        if (profileOverride is not null)
        {
            EncodingProfile effective = profileOverride.Apply(configured: request.Profile, media: mediaInfo);
            if (!ReferenceEquals(objA: effective, objB: request.Profile))
            {
                logger.LogInformation(
                    message: "[{CorrelationId}] Profile overridden by plugin: '{Old}' → '{New}'", args: [context.CorrelationId, request.Profile.Name, effective.Name]
                );
                request = request with { Profile = effective };
            }
        }

        // Stage 2: Validate
        progress?.OnStageStarted(stageName: "Validate");
        ValidateInput validateInput = new(Media: mediaInfo, Profile: request.Profile);
        StageResult validateResult = await validateStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
        if (validateResult is StageFailure validateFailure)
            return Fail(error: validateFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);
        progress?.OnStageCompleted(stageName: "Validate", duration: stopwatch.Elapsed);

        // Stage 3: Plan
        // The smart-orchestrator merge path (DecomposeMergedAsync) already ran
        // PlanAsync once per preset and unioned the results into one OutputPlan
        // BEFORE any child task or the coordinator's FinalizeOnly pass gets here.
        // Re-deriving a plan from this single request's Profile would only see
        // that one preset's renditions — PrecomputedPlan lets every participant
        // in a merged run build its command / master playlist against the exact
        // same merged plan instead. Null (the default) is every other caller —
        // Plan stage runs exactly as before.
        progress?.OnStageStarted(stageName: "Plan");
        ExecutionPlan plan;
        if (request.Options?.PrecomputedPlan is { } precomputedPlan)
        {
            plan = new ExecutionPlan(Groups: [], EstimatedTotalDuration: TimeSpan.Zero, OutputPlan: precomputedPlan);
        }
        else
        {
            StageResult planResult = await planStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
            if (planResult is StageFailure planFailure)
                return Fail(error: planFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);

            plan = ((StageSuccess<ExecutionPlan>)planResult).Value;
        }

        // Update observer with resolved stream info from the actual output plan.
        // Format: "{index}:{detail}" for dashboard display.
        progress?.OnPlanResolved(
            videoStreams: plan.OutputPlan.VideoOutputs.Select(
                    selector: (v, i) => $"{i}:{v.Width}x{v.Height}_{v.EncoderName}"
                )
                .ToList(),
            audioStreams: plan.OutputPlan.AudioOutputs.Select(selector: (a, i) => $"{i}:{a.Language}_{a.CodecToken}")
                .ToList(),
            subtitleStreams: plan.OutputPlan.SubtitleOutputs.Select(selector: (s, i) => $"{i}:{s.Language}_{s.OutputCodec}")
                .ToList(),
            hasGpu: plan.OutputPlan.VideoOutputs.Any(predicate: v =>
                v.EncoderName.Contains(value: "nvenc", comparisonType: StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains(value: "qsv", comparisonType: StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains(value: "amf", comparisonType: StringComparison.OrdinalIgnoreCase)
            ),
            isHdr: mediaInfo.VideoStreams.Count > 0 && mediaInfo.VideoStreams[index: 0].IsHdr
        );
        progress?.OnStageCompleted(stageName: "Plan", duration: stopwatch.Elapsed);

        // Coordinator finalize path: when FinalizeOnly is set the pipeline
        // skips Build + Execute and proceeds straight to FinalizeStage against
        // the variant playlists / segments the child tasks already wrote to
        // the shared tempDir. The synthetic ExecutionResult[] keeps the
        // downstream signature happy — FinalizeStage only reads the count.
        bool finalizeOnly = request.Options?.FinalizeOnly ?? false;
        ExecutionResult[] executionResults;

        if (finalizeOnly)
        {
            executionResults = [];
            logger.LogInformation(
                message: "[{CorrelationId}] FinalizeOnly run — skipping Build + Execute; finalizing against the existing tempDir contents.",
                args: context.CorrelationId
            );
        }
        else
        {
            // Stage 4: Build
            progress?.OnStageStarted(stageName: "Build");
            BuildInput buildInput = new(
                Plan: plan,
                InputPath: request.InputPath,
                OutputDirectory: request.OutputDirectory,
                MediaTitle: request.ResolvedTitle,
                DurationLimit: null,
                Pass: request.Options?.Pass ?? EncodingPass.Single,
                StatsFilePath: request.Options?.StatsFilePath,
                Pass1VariantIndex: request.Options?.Pass1VariantIndex ?? 0,
                TaskFilter: request.Options?.TaskFilter,
                ResumeFromMs: request.Options?.ResumeFromMs
            );
            StageResult buildResult = await buildStage.ExecuteAsync(input: buildInput, context: context, ct: ct);
            if (buildResult is StageFailure buildFailure)
                return Fail(error: buildFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);

            FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)buildResult).Value;
            progress?.OnStageCompleted(stageName: "Build", duration: stopwatch.Elapsed);

            // Stage 5: Execute
            progress?.OnStageStarted(stageName: "Encode");
            ExecuteInput executeInput = new(Commands: commands, InputDuration: mediaInfo.Duration, Progress: progress);
            StageResult executeResult = await executeStage.ExecuteAsync(input: executeInput, context: context, ct: ct);
            if (executeResult is StageFailure executeFailure)
                return Fail(error: executeFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);

            executionResults = ((StageSuccess<ExecutionResult[]>)executeResult).Value;
            progress?.OnStageCompleted(stageName: "Encode", duration: stopwatch.Elapsed);
        }

        // Stage 6: Finalize
        // Per-task runs (TaskFilter set on EncodingOptions) all share the same
        // tempDir. If every per-task EncodeAsync called FinalizeStage they
        // would race on the SAME master playlist, chapters.vtt, fonts.json,
        // manifest.json — concurrent writes to those files corrupt them and
        // the WriteFontManifestAsync path takes an exclusive lock on the
        // fonts/ directory ("being used by another process" in production
        // logs). The coordinator (VideoEncodeJob.HandleFinalizeAsync) runs
        // a FinalizeOnly pass once after every child task is done, so
        // per-task encodes stop at ExecuteStage and return a synthetic
        // FinalizeOutput.
        FinalizeOutput finalizeOutput;
        // A dispatch-time bundled Whole task is the only execution — it must
        // finalize. Only per-stream slices (Video / Audio / Subtitle /
        // Thumbnails) defer FinalizeStage to the coordinator's FinalizeOnly
        // pass to avoid racing the shared master playlist / fonts manifest.
        bool isPerStreamSlice =
            request.Options?.TaskFilter is { } taskFilter
            && taskFilter.Kind != EncodeTaskKind.Whole;
        if (isPerStreamSlice)
        {
            finalizeOutput = new(OutputPath: request.OutputDirectory, OutputSizeBytes: 0);
            logger.LogDebug(
                message: "[{CorrelationId}] Per-task run — skipping FinalizeStage; coordinator handles master playlist, chapters, fonts.json, manifest.",
                args: context.CorrelationId
            );
        }
        else
        {
            progress?.OnStageStarted(stageName: "Finalize");

            // An aux-only bundle (no video and no audio — e.g. a thumbnails top-up)
            // produces no variant playlists, so there is nothing for a master to
            // list. FinalizeStage would otherwise measure the FULL plan's variants
            // against this run's staging directory, find none of them, and fail the
            // task on "Master playlist would list zero variants" — which cost the
            // job its post-encode phase, and with it the subtitle OCR. The master
            // already on disk describes the variants that really exist; leave it be.
            HlsDerivatives? derivatives = request.Profile.HlsDerivatives;
            if (request.Options?.TaskFilter?.LeavesMasterPlaylistAlone == true)
            {
                derivatives = (derivatives ?? new HlsDerivatives()) with
                {
                    GenerateMasterPlaylist = false,
                };
                logger.LogInformation(
                    message: "[{CorrelationId}] Bundle covers a subset of renditions — leaving the existing master playlist untouched.",
                    args: context.CorrelationId
                );
            }

            FinalizeInput finalizeInput = new(
                Results: executionResults,
                Plan: plan.OutputPlan,
                OutputDirectory: request.OutputDirectory,
                MediaTitle: request.ResolvedTitle,
                Progress: progress,
                HlsDerivatives: derivatives,
                Profile: request.Profile
            );
            StageResult finalizeResult = await finalizeStage.ExecuteAsync(
                input: finalizeInput,
                context: context,
                ct: ct
            );
            if (finalizeResult is StageFailure finalizeFailure)
                return Fail(error: finalizeFailure.Error, elapsed: stopwatch.Elapsed, progress: progress);

            finalizeOutput = ((StageSuccess<FinalizeOutput>)finalizeResult).Value;
            progress?.OnStageCompleted(stageName: "Finalize", duration: stopwatch.Elapsed);
        }

        stopwatch.Stop();
        progress?.OnCompleted();

        logger.LogInformation(
            message: "[{CorrelationId}] Encode complete in {Duration}", args: [context.CorrelationId, stopwatch.Elapsed]
        );

        // Metrics from the main encode command (index 0)
        ExecutionMetrics? execMetrics =
            executionResults.Length > 0 ? executionResults[0].Metrics : null;

        string encoderUsed =
            plan.OutputPlan.VideoOutputs.Length > 0
                ? plan.OutputPlan.VideoOutputs[0].EncoderName
                : "audio-only";

        // Resolve GPU name from the encoder — nvenc/qsv/amf indicate hardware encoding
        string? gpuUsed = plan
            .OutputPlan.VideoOutputs.Where(predicate: v =>
                v.EncoderName.Contains(value: "nvenc", comparisonType: StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains(value: "qsv", comparisonType: StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains(value: "amf", comparisonType: StringComparison.OrdinalIgnoreCase)
            )
            .Select(selector: v => v.EncoderName)
            .FirstOrDefault();

        return new(
            Success: true,
            OutputPath: finalizeOutput.OutputPath,
            Duration: stopwatch.Elapsed,
            Error: null,
            Metrics: new(
                OutputSizeBytes: finalizeOutput.OutputSizeBytes,
                AverageSpeed: execMetrics?.AverageSpeed ?? 0,
                AverageFps: execMetrics?.AverageFps ?? 0,
                EncoderUsed: encoderUsed,
                GpuUsed: gpuUsed
            )
        );
    }

    public async Task<PreviewResult> PreviewAsync(
        EncodingRequest request,
        int previewDurationSeconds = 10,
        CancellationToken ct = default
    )
    {
        EncodingContext context = EncodingContext.Create() with
        {
            SourceStorage = request.SourceStorage,
            DestinationStorage = request.DestinationStorage ?? request.SourceStorage,
        };
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimeSpan previewDuration = TimeSpan.FromSeconds(seconds: previewDurationSeconds);

        logger.LogInformation(
            message: "[{CorrelationId}] Starting preview encode ({Duration}s): {Input}", args: [context.CorrelationId, previewDurationSeconds, request.InputPath]
        );

        // Strip thumbnails and subtitles — not useful for a short preview
        EncodingProfile previewProfile = request.Profile with
        {
            Thumbnails = null,
            Subtitles = [],
        };
        EncodingRequest previewRequest = request with { Profile = previewProfile };

        // Stage 1: Analyze
        StageResult analyzeResult = await analyzeStage.ExecuteAsync(
            input: previewRequest.InputPath,
            context: context,
            ct: ct
        );
        if (analyzeResult is StageFailure af)
            return PreviewFail(error: af.Error, elapsed: stopwatch.Elapsed);

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };

        // Stage 2: Validate
        ValidateInput validateInput = new(Media: mediaInfo, Profile: previewProfile);
        StageResult validateResult = await validateStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
        if (validateResult is StageFailure vf)
            return PreviewFail(error: vf.Error, elapsed: stopwatch.Elapsed);

        // Stage 3: Plan
        StageResult planResult = await planStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
        if (planResult is StageFailure pf)
            return PreviewFail(error: pf.Error, elapsed: stopwatch.Elapsed);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)planResult).Value;

        // Stage 4: Build — with duration limit
        BuildInput buildInput = new(
            Plan: plan,
            InputPath: previewRequest.InputPath,
            OutputDirectory: previewRequest.OutputDirectory,
            MediaTitle: previewRequest.ResolvedTitle,
            DurationLimit: previewDuration
        );
        StageResult buildResult = await buildStage.ExecuteAsync(input: buildInput, context: context, ct: ct);
        if (buildResult is StageFailure bf)
            return PreviewFail(error: bf.Error, elapsed: stopwatch.Elapsed);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)buildResult).Value;

        // Stage 5: Execute — duration-limited input means short encode
        ExecuteInput executeInput = new(Commands: commands, InputDuration: previewDuration);
        StageResult executeResult = await executeStage.ExecuteAsync(input: executeInput, context: context, ct: ct);
        if (executeResult is StageFailure ef)
            return PreviewFail(error: ef.Error, elapsed: stopwatch.Elapsed);

        ExecutionResult[] executionResults = ((StageSuccess<ExecutionResult[]>)executeResult).Value;

        // Stage 6: Finalize
        FinalizeInput finalizeInput = new(
            Results: executionResults,
            Plan: plan.OutputPlan,
            OutputDirectory: previewRequest.OutputDirectory,
            MediaTitle: previewRequest.ResolvedTitle,
            HlsDerivatives: previewRequest.Profile.HlsDerivatives
        );
        StageResult finalizeResult = await finalizeStage.ExecuteAsync(input: finalizeInput, context: context, ct: ct);
        if (finalizeResult is StageFailure ff)
            return PreviewFail(error: ff.Error, elapsed: stopwatch.Elapsed);

        FinalizeOutput finalizeOutput = ((StageSuccess<FinalizeOutput>)finalizeResult).Value;
        stopwatch.Stop();

        ExecutionMetrics? execMetrics =
            executionResults.Length > 0 ? executionResults[0].Metrics : null;

        string encoderUsed =
            plan.OutputPlan.VideoOutputs.Length > 0
                ? plan.OutputPlan.VideoOutputs[0].EncoderName
                : "audio-only";

        logger.LogInformation(
            message: "[{CorrelationId}] Preview encode complete in {Duration}", args: [context.CorrelationId, stopwatch.Elapsed]
        );

        return new(
            Success: true,
            OutputPath: finalizeOutput.OutputPath,
            Duration: stopwatch.Elapsed,
            Metrics: new(
                OutputSizeBytes: finalizeOutput.OutputSizeBytes,
                AverageSpeed: execMetrics?.AverageSpeed ?? 0,
                AverageFps: execMetrics?.AverageFps ?? 0,
                EncoderUsed: encoderUsed,
                GpuUsed: null
            ),
            OutputSizeBytes: finalizeOutput.OutputSizeBytes,
            Error: null
        );
    }

    public async Task<OutputPlan?> PlanAsync(
        EncodingRequest request,
        CancellationToken ct = default
    )
    {
        EncodingContext context = EncodingContext.Create() with
        {
            SourceStorage = request.SourceStorage,
            DestinationStorage = request.DestinationStorage ?? request.SourceStorage,
            // Same identity EncodeAsync threads through. PlanStage resolves the
            // BundleLayout from it, and a plan without one carries no manifest or
            // reconstruction path — so dropping it here produced plans that could
            // never describe or revert themselves, no matter what the caller set.
            MediaItem = request.MediaItem,
            OriginalInputPath = request.OriginalInputPath,
            OriginalOutputDirectory = request.OriginalOutputDirectory,
        };

        StageResult analyzeResult = await analyzeStage.ExecuteAsync(input: request.InputPath, context: context, ct: ct);
        if (analyzeResult is StageFailure)
            return null;

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };

        ValidateInput validateInput = new(Media: mediaInfo, Profile: request.Profile);
        StageResult validateResult = await validateStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
        if (validateResult is StageFailure)
            return null;

        StageResult planResult = await planStage.ExecuteAsync(input: validateInput, context: context, ct: ct);
        if (planResult is StageFailure)
            return null;

        return ((StageSuccess<ExecutionPlan>)planResult).Value.OutputPlan;
    }

    private static PreviewResult PreviewFail(EncodingError error, TimeSpan elapsed)
    {
        return new(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "", GpuUsed: null),
            OutputSizeBytes: 0,
            Error: error
        );
    }

    private static EncodingResult Fail(
        EncodingError error,
        TimeSpan elapsed,
        IProgressObserver? progress
    )
    {
        progress?.OnError(error: error);
        return new(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Error: error,
            Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "", GpuUsed: null)
        );
    }
}
