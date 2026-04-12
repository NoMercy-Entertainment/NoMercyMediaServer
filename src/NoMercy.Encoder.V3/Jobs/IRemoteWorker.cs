namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Pipeline;
using NoMercy.Encoder.V3.Progress;

public interface IRemoteWorker
{
    string WorkerId { get; }

    Task<EncodingResult> ExecuteJobAsync(
        EncodingJob job,
        IProgress<EncodingProgress> progress,
        CancellationToken ct
    );

    IHardwareCapabilities GetCapabilities();
}
