namespace NoMercy.Encoder.V3.Pipeline;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Errors;
using NoMercy.Encoder.V3.Execution;
using NoMercy.Encoder.V3.Pipeline.Stages;
using NoMercy.Encoder.V3.Progress;

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

        progress?.OnProgress(
            new EncodingProgress(
                context.CorrelationId,
                0,
                TimeSpan.Zero,
                null,
                null,
                null,
                "Analyze",
                null
            )
        );

        // Stage 1: Analyze
        StageResult analyzeResult = await analyzeStage.ExecuteAsync(request.InputPath, context, ct);
        if (analyzeResult is StageFailure analyzeFailure)
            return Fail(analyzeFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        MediaInfo mediaInfo = ((StageSuccess<MediaInfo>)analyzeResult).Value;
        context = context with { MediaInfo = mediaInfo };

        // Stage 2: Validate
        ValidateInput validateInput = new(mediaInfo, request.Profile);
        StageResult validateResult = await validateStage.ExecuteAsync(validateInput, context, ct);
        if (validateResult is StageFailure validateFailure)
            return Fail(validateFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        // Stage 3: Plan
        StageResult planResult = await planStage.ExecuteAsync(validateInput, context, ct);
        if (planResult is StageFailure planFailure)
            return Fail(planFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        ExecutionPlan plan = ((StageSuccess<ExecutionPlan>)planResult).Value;

        // Stage 4: Build
        BuildInput buildInput = new(plan, request.InputPath, request.OutputDirectory);
        StageResult buildResult = await buildStage.ExecuteAsync(buildInput, context, ct);
        if (buildResult is StageFailure buildFailure)
            return Fail(buildFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        FfmpegCommand[] commands = ((StageSuccess<FfmpegCommand[]>)buildResult).Value;

        // Stage 5: Execute
        ExecuteInput executeInput = new(commands, mediaInfo.Duration);
        StageResult executeResult = await executeStage.ExecuteAsync(executeInput, context, ct);
        if (executeResult is StageFailure executeFailure)
            return Fail(executeFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        ExecutionResult[] executionResults = ((StageSuccess<ExecutionResult[]>)executeResult).Value;

        // Stage 6: Finalize
        FinalizeInput finalizeInput = new(
            executionResults,
            plan.OutputPlan,
            request.OutputDirectory
        );
        StageResult finalizeResult = await finalizeStage.ExecuteAsync(finalizeInput, context, ct);
        if (finalizeResult is StageFailure finalizeFailure)
            return Fail(finalizeFailure.Error, stopwatch.Elapsed, context.CorrelationId, progress);

        FinalizeOutput finalizeOutput = ((StageSuccess<FinalizeOutput>)finalizeResult).Value;

        stopwatch.Stop();
        progress?.OnCompleted(context.CorrelationId);

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

    private static EncodingResult Fail(
        EncodingError error,
        TimeSpan elapsed,
        string correlationId,
        IProgressObserver? progress
    )
    {
        progress?.OnError(correlationId, error.Message);
        return new EncodingResult(
            Success: false,
            OutputPath: "",
            Duration: elapsed,
            Error: error,
            Metrics: new EncodingMetrics(0, 0, 0, "", null)
        );
    }
}
