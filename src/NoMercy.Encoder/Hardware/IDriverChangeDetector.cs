namespace NoMercy.Encoder.Hardware;

public record DriverChangeResult(
    string CurrentHash,
    string? PreviousHash,
    bool Changed,
    bool IsFirstBoot
);

public interface IDriverChangeDetector
{
    /// <summary>
    /// Builds a fingerprint from the current GPU set, compares it to the previously
    /// persisted hash, saves the new hash, and returns the comparison result.
    /// </summary>
    Task<DriverChangeResult> DetectAndPersistAsync(CancellationToken ct = default);
}
