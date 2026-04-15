namespace NoMercy.Encoder.Pipeline;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Execution;
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Progress;

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
        EncodingContext context = EncodingContext.Create();
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
            request.ResolvedTitle
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
            request.ResolvedTitle
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

        return new EncodingResult(
            Success: true,
            OutputPath: finalizeOutput.OutputPath,
            Duration: stopwatch.Elapsed,
            Error: null,
            Metrics: new EncodingMetrics(
                OutputSizeBytes: finalizeOutput.OutputSizeBytes,
                AverageSpeed: 0,
                AverageFps: 0,
                EncoderUsed: plan.OutputPlan.VideoOutputs.Length > 0
                    ? plan.OutputPlan.VideoOutputs[0].EncoderName
                    : "audio-only",
                GpuUsed: null
            )
        );
    }

    public Task<PreviewResult> PreviewAsync(
        EncodingRequest request,
        int previewDurationSeconds = 10,
        CancellationToken ct = default
    )
    {
        throw new NotImplementedException("Preview encoding not yet implemented");
    }

    private static EncodingResult Fail(
        EncodingError error,
        TimeSpan elapsed,
        IProgressObserver? progress
    )
    {
        progress?.OnError(error);
        return new EncodingResult(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Error: error,
            Metrics: new EncodingMetrics(0, 0, 0, "", null)
        );
    }
}
