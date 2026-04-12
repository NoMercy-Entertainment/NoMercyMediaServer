namespace NoMercy.Encoder.V3.Audio;

public interface IAudioFingerprinter
{
    Task<string> GetFingerprintAsync(string audioPath, CancellationToken ct);

    Task<FingerprintMatch[]> IdentifyAsync(
        string fingerprint,
        int durationSeconds,
        CancellationToken ct
    );
}

public record FingerprintMatch(
    string AcoustId,
    double Score,
    string? MusicBrainzRecordingId,
    string? Title,
    string? Artist,
    string? Album
);
