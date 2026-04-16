namespace NoMercy.Encoder.Execution;

/// <summary>
/// Tracks live FFmpeg process IDs per encode job so the dashboard (or any
/// out-of-band caller) can locate them for pause / resume / cancel operations.
/// Keyed by the public job id that the dashboard uses — for video encodes this
/// is the movie / episode id, matching V1 behavior.
/// </summary>
public interface IEncoderProcessRegistry
{
    /// <summary>
    /// Record that <paramref name="processId"/> is running for <paramref name="jobId"/>.
    /// Idempotent — registering the same pid twice is a no-op.
    /// </summary>
    void Register(int jobId, int processId);

    /// <summary>
    /// Remove a single process mapping. Call when the process exits.
    /// </summary>
    void Unregister(int jobId, int processId);

    /// <summary>
    /// Remove all processes for a job. Call when the job completes or fails.
    /// </summary>
    void UnregisterJob(int jobId);

    /// <summary>
    /// Returns the process ids currently running for a job, or an empty set
    /// when the job is not running.
    /// </summary>
    IReadOnlyCollection<int> GetProcessIds(int jobId);

    /// <summary>
    /// All registered job ids currently known to the registry.
    /// </summary>
    IReadOnlyCollection<int> ActiveJobIds { get; }
}
