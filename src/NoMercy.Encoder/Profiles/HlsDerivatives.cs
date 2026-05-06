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
}
