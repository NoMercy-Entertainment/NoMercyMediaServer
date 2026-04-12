namespace NoMercy.Encoder.V3.Audio;

public interface IAudioMetadataWriter
{
    Task WriteTagsAsync(string filePath, AudioMetadata metadata, CancellationToken ct);
}

public record AudioMetadata(
    string Title,
    string Artist,
    string AlbumArtist,
    string Album,
    int TrackNumber,
    int DiscNumber,
    int? Year,
    string? Genre,
    string? MusicBrainzTrackId,
    string? MusicBrainzReleaseId,
    string? AcoustIdFingerprint,
    AlbumArtSource? CoverArt
);

public record AlbumArtSource(string? FilePath, string? Url, AlbumArtType Type);

public enum AlbumArtType
{
    Front,
    Back,
    Disc,
    Artist,
    Other,
}
