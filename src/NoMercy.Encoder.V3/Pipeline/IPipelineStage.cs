namespace NoMercy.Encoder.V3.Pipeline;

public interface IPipelineStage<TInput, TOutput>
{
    string Name { get; }

    Task<StageResult> ExecuteAsync(
        TInput input,
        EncodingContext context,
        CancellationToken ct = default
    );
}
