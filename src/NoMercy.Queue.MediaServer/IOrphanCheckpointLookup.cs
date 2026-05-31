namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Checks whether a resumable crash checkpoint exists for an encoder queue job.
/// Implemented by the encoder layer and registered in DI at startup; the orphan
/// recovery service uses this to distinguish jobs that can be re-queued for
/// resume from those that must be failed outright.
/// </summary>
public interface IOrphanCheckpointLookup
{
    /// <summary>
    /// Returns true when a crash checkpoint exists for the given job payload
    /// (i.e., a previous run saved progress data for this job's output directory).
    /// </summary>
    Task<bool> HasCheckpointAsync(string jobPayload, CancellationToken ct = default);
}
