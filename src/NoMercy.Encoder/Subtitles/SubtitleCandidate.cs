namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Normalized subtitle candidate returned by any acquisition provider.
/// Encoder code works exclusively with this record — never with provider DTOs.
/// </summary>
public record SubtitleCandidate(
    string Provider,
    string Language,
    double Rating,
    int Downloads,
    bool IsTrustedUploader,
    double? Fps,
    string DownloadUrl,
    string Format
);
