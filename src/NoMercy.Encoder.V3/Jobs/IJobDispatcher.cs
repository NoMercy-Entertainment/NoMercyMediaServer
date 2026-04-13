namespace NoMercy.Encoder.V3.Jobs;

using NoMercy.Encoder.V3.Progress;

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
