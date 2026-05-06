using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Pipeline;

public class Encoder(
    AnalyzeStage analyzeStage,
    ValidateStage validateStage,
    PlanStage planStage,
    BuildStage buildStage,
    ExecuteStage executeStage,
    FinalizeStage finalizeStage,
    ILogger<Encoder> logger
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
        };
        Stopwatch stopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "[{CorrelationId}] Starting encode: {Input} → {Output}",
            context.CorrelationId,
            request.InputPath,
            request.OutputDirectory
        );

        // Stage 1: Analyze
        progress?.OnStageStarted("Analyze");
        StageResult analyzeResult = await analyzeStage.ExecuteAsync(request.InputPath, context, ct);
        if (analyzeResult is StageFailure analyzeFailure)
            return Fail(analyzeFailure.Error, stopwatch.Elapsed, progress);

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };
        progress?.OnStageCompleted("Analyze", stopwatch.Elapsed);

        // Stage 2: Validate
        progress?.OnStageStarted("Validate");
        ValidateInput validateInput = new(mediaInfo, request.Profile);
        StageResult validateResult = await validateStage.ExecuteAsync(validateInput, context, ct);
        if (validateResult is StageFailure validateFailure)
            return Fail(validateFailure.Error, stopwatch.Elapsed, progress);
        progress?.OnStageCompleted("Validate", stopwatch.Elapsed);

        // Stage 3: Plan
        progress?.OnStageStarted("Plan");
        StageResult planResult = await planStage.ExecuteAsync(validateInput, context, ct);
        if (planResult is StageFailure planFailure)
            return Fail(planFailure.Error, stopwatch.Elapsed, progress);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)planResult).Value;

        // Update observer with resolved stream info from the actual output plan.
        // Format matches V1: "{index}:{detail}" for dashboard display.
        progress?.OnPlanResolved(
            plan.OutputPlan.VideoOutputs.Select(
                    (v, i) => $"{i}:{v.Width}x{v.Height}_{v.EncoderName}"
                )
                .ToList(),
            plan.OutputPlan.AudioOutputs.Select(
                    (a, i) =>
                        $"{i}:{a.Language}_{a.EncoderName.Replace("libfdk_", "").Replace("lib", "")}"
                )
                .ToList(),
            plan.OutputPlan.SubtitleOutputs.Select((s, i) => $"{i}:{s.Language}_{s.OutputCodec}")
                .ToList(),
            hasGpu: plan.OutputPlan.VideoOutputs.Any(v =>
                v.EncoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains("qsv", StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains("amf", StringComparison.OrdinalIgnoreCase)
            ),
            isHdr: mediaInfo.VideoStreams.Count > 0 && mediaInfo.VideoStreams[0].IsHdr
        );
        progress?.OnStageCompleted("Plan", stopwatch.Elapsed);

        // Stage 4: Build
        progress?.OnStageStarted("Build");
        BuildInput buildInput = new(
            plan,
            request.InputPath,
            request.OutputDirectory,
            request.ResolvedTitle,
            DurationLimit: null,
            Pass: request.Options?.Pass ?? EncodingPass.Single,
            StatsFilePath: request.Options?.StatsFilePath,
            Pass1VariantIndex: request.Options?.Pass1VariantIndex ?? 0
        );
        StageResult buildResult = await buildStage.ExecuteAsync(buildInput, context, ct);
        if (buildResult is StageFailure buildFailure)
            return Fail(buildFailure.Error, stopwatch.Elapsed, progress);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)buildResult).Value;
        progress?.OnStageCompleted("Build", stopwatch.Elapsed);

        // Stage 5: Execute
        progress?.OnStageStarted("Encode");
        ExecuteInput executeInput = new(commands, mediaInfo.Duration, progress);
        StageResult executeResult = await executeStage.ExecuteAsync(executeInput, context, ct);
        if (executeResult is StageFailure executeFailure)
            return Fail(executeFailure.Error, stopwatch.Elapsed, progress);

        ExecutionResult[] executionResults = ((StageSuccess<ExecutionResult[]>)executeResult).Value;
        progress?.OnStageCompleted("Encode", stopwatch.Elapsed);

        // Stage 6: Finalize
        progress?.OnStageStarted("Finalize");
        FinalizeInput finalizeInput = new(
            executionResults,
            plan.OutputPlan,
            request.OutputDirectory,
            request.ResolvedTitle,
            Progress: progress,
            HlsDerivatives: request.Profile.HlsDerivatives
        );
        StageResult finalizeResult = await finalizeStage.ExecuteAsync(finalizeInput, context, ct);
        if (finalizeResult is StageFailure finalizeFailure)
            return Fail(finalizeFailure.Error, stopwatch.Elapsed, progress);

        FinalizeOutput finalizeOutput = ((StageSuccess<FinalizeOutput>)finalizeResult).Value;

        stopwatch.Stop();
        progress?.OnStageCompleted("Finalize", stopwatch.Elapsed);
        progress?.OnCompleted();

        logger.LogInformation(
            "[{CorrelationId}] Encode complete in {Duration}",
            context.CorrelationId,
            stopwatch.Elapsed
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
            .OutputPlan.VideoOutputs.Where(v =>
                v.EncoderName.Contains("nvenc", StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains("qsv", StringComparison.OrdinalIgnoreCase)
                || v.EncoderName.Contains("amf", StringComparison.OrdinalIgnoreCase)
            )
            .Select(v => v.EncoderName)
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
        TimeSpan previewDuration = TimeSpan.FromSeconds(previewDurationSeconds);

        logger.LogInformation(
            "[{CorrelationId}] Starting preview encode ({Duration}s): {Input}",
            context.CorrelationId,
            previewDurationSeconds,
            request.InputPath
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
            previewRequest.InputPath,
            context,
            ct
        );
        if (analyzeResult is StageFailure af)
            return PreviewFail(af.Error, stopwatch.Elapsed);

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };

        // Stage 2: Validate
        ValidateInput validateInput = new(mediaInfo, previewProfile);
        StageResult validateResult = await validateStage.ExecuteAsync(validateInput, context, ct);
        if (validateResult is StageFailure vf)
            return PreviewFail(vf.Error, stopwatch.Elapsed);

        // Stage 3: Plan
        StageResult planResult = await planStage.ExecuteAsync(validateInput, context, ct);
        if (planResult is StageFailure pf)
            return PreviewFail(pf.Error, stopwatch.Elapsed);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)planResult).Value;

        // Stage 4: Build — with duration limit
        BuildInput buildInput = new(
            plan,
            previewRequest.InputPath,
            previewRequest.OutputDirectory,
            previewRequest.ResolvedTitle,
            DurationLimit: previewDuration
        );
        StageResult buildResult = await buildStage.ExecuteAsync(buildInput, context, ct);
        if (buildResult is StageFailure bf)
            return PreviewFail(bf.Error, stopwatch.Elapsed);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)buildResult).Value;

        // Stage 5: Execute — duration-limited input means short encode
        ExecuteInput executeInput = new(commands, previewDuration);
        StageResult executeResult = await executeStage.ExecuteAsync(executeInput, context, ct);
        if (executeResult is StageFailure ef)
            return PreviewFail(ef.Error, stopwatch.Elapsed);

        ExecutionResult[] executionResults = ((StageSuccess<ExecutionResult[]>)executeResult).Value;

        // Stage 6: Finalize
        FinalizeInput finalizeInput = new(
            executionResults,
            plan.OutputPlan,
            previewRequest.OutputDirectory,
            previewRequest.ResolvedTitle,
            HlsDerivatives: previewRequest.Profile.HlsDerivatives
        );
        StageResult finalizeResult = await finalizeStage.ExecuteAsync(finalizeInput, context, ct);
        if (finalizeResult is StageFailure ff)
            return PreviewFail(ff.Error, stopwatch.Elapsed);

        FinalizeOutput finalizeOutput = ((StageSuccess<FinalizeOutput>)finalizeResult).Value;
        stopwatch.Stop();

        ExecutionMetrics? execMetrics =
            executionResults.Length > 0 ? executionResults[0].Metrics : null;

        string encoderUsed =
            plan.OutputPlan.VideoOutputs.Length > 0
                ? plan.OutputPlan.VideoOutputs[0].EncoderName
                : "audio-only";

        logger.LogInformation(
            "[{CorrelationId}] Preview encode complete in {Duration}",
            context.CorrelationId,
            stopwatch.Elapsed
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

    private static PreviewResult PreviewFail(EncodingError error, TimeSpan elapsed)
    {
        return new(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Metrics: new(0, 0, 0, "", null),
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
        progress?.OnError(error);
        return new(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Error: error,
            Metrics: new(0, 0, 0, "", null)
        );
    }
}
