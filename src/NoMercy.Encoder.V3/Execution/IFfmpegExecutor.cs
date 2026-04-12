namespace NoMercy.Encoder.V3.Execution;

using NoMercy.Encoder.V3.Commands;
using NoMercy.Encoder.V3.Progress;

public interface IFfmpegExecutor
{
    Task<ExecutionResult> ExecuteAsync(
        FfmpegCommand command,
        TimeSpan inputDuration,
        Action<EncodingProgress>? onProgress = null,
        string? correlationId = null,
        CancellationToken ct = default
    );
}
