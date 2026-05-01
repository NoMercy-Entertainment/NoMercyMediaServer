using NoMercy.Encoder.Progress;

namespace NoMercy.Encoder.Jobs;

public interface IJobDispatcher
{
    Task<RemoteEncodingResult> DispatchAsync(
        EncodingJob job,
        IProgress<EncodingProgress> progress,
        CancellationToken ct
    );

    IReadOnlyList<IRemoteWorker> AvailableWorkers { get; }

    IRemoteWorker? SelectWorker(EncodingJob job);
}
