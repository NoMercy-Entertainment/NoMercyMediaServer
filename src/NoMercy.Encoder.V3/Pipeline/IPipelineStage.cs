namespace NoMercy.Encoder.V3.Pipeline;

// Non-generic marker interface used by plugin hooks
public interface IPipelineStage
{
    string Name { get; }
}

public interface IPipelineStage<TInput, TOutput> : IPipelineStage
{
    Task<StageResult> ExecuteAsync(
        TInput input,
        EncodingContext context,
        CancellationToken ct = default
    );
}
