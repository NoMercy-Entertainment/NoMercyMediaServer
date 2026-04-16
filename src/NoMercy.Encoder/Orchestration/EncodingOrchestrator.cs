namespace NoMercy.Encoder.Orchestration;

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;

public class EncodingOrchestrator(IStrategyResolver resolver, ILogger<EncodingOrchestrator> logger)
    : IEncodingOrchestrator
{
    public async Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    )
    {
        IEncodingStrategy? strategy = resolver.Resolve(
            request.Profile.Format,
            request.Profile.EncodeMode
        );

        if (strategy is null)
        {
            EncodingError error = new(
                Kind: EncodingErrorKind.Unknown,
                Message: $"No strategy registered for {request.Profile.Format} / "
                    + $"{request.Profile.EncodeMode}. Register an IEncodingStrategy implementation "
                    + "for this combination.",
                FfmpegStderr: null,
                StageName: "Orchestrator",
                Recoverable: false
            );
            progress?.OnError(error);
            return new EncodingResult(
                Success: false,
                OutputPath: string.Empty,
                Duration: TimeSpan.Zero,
                Error: error,
                Metrics: new EncodingMetrics(0, 0, 0, string.Empty, null)
            );
        }

        logger.LogInformation(
            "Dispatching to {Strategy} ({Format}/{Mode}) for {Input}",
            strategy.GetType().Name,
            strategy.Format,
            strategy.EncodeMode,
            request.InputPath
        );

        return await strategy.EncodeAsync(request, progress, ct);
    }
}
