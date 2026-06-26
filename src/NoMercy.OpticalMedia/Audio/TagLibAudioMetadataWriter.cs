using NoMercy.Encoder.Audio;
using TagLib;

namespace NoMercy.OpticalMedia.Audio;

/// <summary>
/// Writes ID3 / Vorbis / APE tags to an audio file via TagLib#.
/// Used after CD ripping so the FLAC files carry MusicBrainz-sourced
/// metadata before AudioImportJob reads them.
/// </summary>
public sealed class TagLibAudioMetadataWriter : IAudioMetadataWriter
{
    public async Task WriteTagsAsync(string filePath, AudioMetadata metadata, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[]? coverBytes = await ResolveCoverBytesAsync(metadata.CoverArt, ct);

        ct.ThrowIfCancellationRequested();

        using TagLib.File tagFile = TagLib.File.Create(filePath);
        Tag tag = tagFile.Tag;

        tag.Title = metadata.Title;
        tag.Performers = [metadata.Artist];
        tag.AlbumArtists = [metadata.AlbumArtist];
        tag.Album = metadata.Album;
        tag.Track = (uint)metadata.TrackNumber;
        tag.TrackCount = 0;
        tag.Disc = (uint)metadata.DiscNumber;
        tag.Year = metadata.Year is { } year ? (uint)year : 0;
        tag.Genres = metadata.Genre is not null ? [metadata.Genre] : [];
        tag.MusicBrainzTrackId = metadata.MusicBrainzTrackId;
        tag.MusicBrainzReleaseId = metadata.MusicBrainzReleaseId;

        if (coverBytes is { Length: > 0 })
        {
            tag.Pictures =
            [
                new Picture(new ByteVector(coverBytes))
                {
                    Type = PictureType.FrontCover,
                    MimeType = "image/jpeg",
                },
            ];
        }

        tagFile.Save();
    }

    private static async Task<byte[]?> ResolveCoverBytesAsync(
        AlbumArtSource? source,
        CancellationToken ct
    )
    {
        if (source is null)
            return null;

        if (source.FilePath is not null && System.IO.File.Exists(source.FilePath))
            return await System.IO.File.ReadAllBytesAsync(source.FilePath, ct);

        if (source.Url is not null)
        {
            try
            {
                using HttpClient httpClient = new();
                return await httpClient.GetByteArrayAsync(source.Url, ct);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
