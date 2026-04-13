namespace NoMercy.Encoder.Jobs;

using NoMercy.Encoder.Progress;

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
