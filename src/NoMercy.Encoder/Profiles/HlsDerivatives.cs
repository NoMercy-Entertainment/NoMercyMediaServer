namespace NoMercy.Encoder.Profiles;

public record HlsDerivatives
{
    public bool GenerateMetadataJson { get; init; } = true;
    public bool GenerateSpriteVtt { get; init; } = true;
    public int SpriteVttIntervalSeconds { get; init; } = 10;
    public int SpriteVttColumns { get; init; } = 5;
    public int SpriteVttRows { get; init; } = 5;
    public int SpriteVttThumbnailWidth { get; init; } = 160;
    public bool GenerateChapters { get; init; } = true;
    public bool GenerateFontsJson { get; init; } = true;
    public bool GenerateIFramePlaylists { get; init; }
    public bool GenerateThumbnailTrack { get; init; } = true;
    public bool ExtractClosedCaptions { get; init; }
    public bool GenerateMasterPlaylist { get; init; } = true;
    public bool WriteOriginalFilename { get; init; } = true;

    /// <summary>
    /// When true, the encoder emits one still per chapter at the chapter's exact
    /// timestamp. Output: <c>chapters/{NN}.webp</c> referenced from
    /// <c>chapters.vtt</c>. When false (default), the player falls back to the
    /// existing thumbs sprite frame nearest each chapter.
    /// </summary>
    public bool GenerateChapterThumbs { get; init; } = false;

    /// <summary>
    /// Slice extracted WebVTT subtitles into HLS-style segments + a per-track
    /// media playlist (<c>subtitles/{lang}/{variant}.m3u8</c>). When false, the
    /// raw <c>.vtt</c> extract still lands on disk for download, but the master
    /// playlist omits the EXT-X-MEDIA subtitle entry and no segments are
    /// written. Default true — every HLS profile shipped expects chunked
    /// subtitle delivery.
    /// </summary>
    public bool SubtitleWebVtt { get; init; } = true;

    /// <summary>
    /// IMSC subtitle track output is not yet implemented. The flag exists so
    /// the dashboard editor can persist user intent, but FinalizeStage throws
    /// when it is set so a profile doesn't silently produce a missing artifact.
    /// </summary>
    public bool SubtitleImsc { get; init; } = false;
}
