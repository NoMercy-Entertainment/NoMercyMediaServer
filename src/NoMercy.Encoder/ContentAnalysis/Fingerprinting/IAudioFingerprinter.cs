namespace NoMercy.Encoder.ContentAnalysis.Fingerprinting;

/// <summary>
/// Extracts a chromaprint audio fingerprint from a file. Windowed — the
/// intro detector only needs the first few minutes, and the outro detector
/// only the last few. Fingerprinting the whole file would be wasteful.
/// </summary>
public interface IAudioFingerprinter
{
    /// <summary>
    /// Produce a fingerprint for the given window. Pass <c>null</c> for
    /// <paramref name="window"/> to fingerprint the entire file (useful
    /// for short snippets; avoid for full feature-length content).
    /// </summary>
    Task<AudioFingerprint> FingerprintAsync(
        string filePath,
        FingerprintWindow? window,
        CancellationToken ct
    );
}
