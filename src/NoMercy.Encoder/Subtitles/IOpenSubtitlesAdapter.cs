namespace NoMercy.Encoder.Subtitles;

/// <summary>
/// Encoder-facing adapter for OpenSubtitles. Translates between encoder
/// records and provider DTOs so encoder code never imports provider types.
/// </summary>
public interface IOpenSubtitlesAdapter
{
    Task<IReadOnlyList<SubtitleCandidate>> SearchByHashAsync(
        string movieHash,
        long fileSize,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false
    );

    Task<IReadOnlyList<SubtitleCandidate>> SearchByFilenameAsync(
        string filename,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false
    );

    Task<IReadOnlyList<SubtitleCandidate>> SearchByTitleAsync(
        string title,
        int? season,
        int? episode,
        int? year,
        string[] languages,
        TimeSpan timeout,
        CancellationToken ct,
        bool trustedOnly = false
    );

    Task<byte[]> DownloadAsync(SubtitleCandidate candidate, CancellationToken ct);

    bool IsRateLimited { get; }
}
