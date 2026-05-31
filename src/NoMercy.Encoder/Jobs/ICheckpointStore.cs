namespace NoMercy.Encoder.Jobs;

/// <summary>
/// Persists encode job checkpoints so a crashed or cancelled job can resume
/// without redoing completed work (e.g. skip pass 1 in a 2-pass encode).
/// Storage is keyed by output directory — the checkpoint lives next to the
/// encode it describes, so multiple concurrent jobs cannot collide.
/// </summary>
public interface ICheckpointStore
{
    Task SaveAsync(JobCheckpoint checkpoint, CancellationToken ct = default);
    Task<JobCheckpoint?> LoadAsync(string outputDirectory, CancellationToken ct = default);
    Task DeleteAsync(string outputDirectory, CancellationToken ct = default);
}
