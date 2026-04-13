namespace NoMercy.Encoder.Execution;

using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Progress;

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
