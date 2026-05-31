namespace NoMercy.Encoder.Hardware;

public interface IDriverFingerprintStore
{
    /// <summary>
    /// Loads the previously persisted driver fingerprint hash.
    /// Returns null when the file is missing or corrupt.
    /// </summary>
    Task<string?> LoadHashAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the current driver fingerprint hash to durable storage.
    /// </summary>
    Task SaveHashAsync(string hash, CancellationToken ct = default);
}
