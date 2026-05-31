using NoMercy.Encoder.Pipeline;

namespace NoMercy.Encoder.Jobs;

public record BatchEncodingRequest(EncodingRequest[] Items, BatchOptions Options);

public record BatchOptions(
    bool ShareAnalysis = true,
    bool ParallelEncoding = false,
    int MaxParallel = 1,
    BatchCancellationMode CancelMode = BatchCancellationMode.SkipRemaining
);

public enum BatchCancellationMode
{
    SkipRemaining,
    CancelAll,
    CancelAndClean,
}
