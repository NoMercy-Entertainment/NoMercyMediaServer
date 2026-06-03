using NoMercy.Encoder.Audio;
using NoMercy.OpticalMedia.Audio;
using TagLib;

namespace NoMercy.Tests.OpticalMedia.Rip;

/// <summary>
/// Round-trip tests for <see cref="TagLibAudioMetadataWriter"/>.
/// A minimal silent FLAC is written to a temp file; each test writes tags
/// via the writer then re-opens the file with TagLib# to assert the field
/// values survived the round-trip.
///
/// The minimal FLAC byte sequence below contains a valid STREAMINFO block
/// (44100 Hz, stereo, 16-bit, 0 samples) so TagLib# accepts it as a FLAC.
/// </summary>
[Trait("Category", "Unit")]
public class TagLibAudioMetadataWriterTests : IDisposable
{
    // Minimal valid FLAC: fLaC marker + STREAMINFO metadata block (34 bytes).
    // Bit-exact STREAMINFO: min/max blocksize=4096, min/max framesize=0,
    // sample_rate=44100, channels=2, bits=16, total_samples=0, md5=zeros.
    // 0x80 block-type byte = last-metadata-block (1) + type STREAMINFO (0).
    private static readonly byte[] MinimalFlacBytes = Convert.FromBase64String(
        "ZkxhQ4AAACIQABAAAAAAAAAACsRC8AAAAAAAAAAAAAAAAAAAAAAAAAAA"
    );

    private readonly string _tempFlac;

    public TagLibAudioMetadataWriterTests()
    {
        _tempFlac = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.flac");
        System.IO.File.WriteAllBytes(_tempFlac, MinimalFlacBytes);
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_tempFlac))
            System.IO.File.Delete(_tempFlac);
        GC.SuppressFinalize(this);
    }

    private async Task WriteAndReopenAsync(AudioMetadata metadata, Action<Tag> assertions)
    {
        TagLibAudioMetadataWriter writer = new();
        await writer.WriteTagsAsync(_tempFlac, metadata, CancellationToken.None);

        using TagLib.File tagFile = TagLib.File.Create(_tempFlac);
        assertions(tagFile.Tag);
    }

    private static AudioMetadata BasicMetadata(
        string title = "Test Title",
        string artist = "Test Artist",
        string albumArtist = "Album Artist",
        string album = "Test Album",
        int trackNumber = 3,
        int discNumber = 1,
        int? year = 2024,
        string? genre = "Rock",
        string? trackId = "rec-mbid-001",
        string? releaseId = "rel-mbid-001",
        AlbumArtSource? coverArt = null
    ) =>
        new(
            Title: title,
            Artist: artist,
            AlbumArtist: albumArtist,
            Album: album,
            TrackNumber: trackNumber,
            DiscNumber: discNumber,
            Year: year,
            Genre: genre,
            MusicBrainzTrackId: trackId,
            MusicBrainzReleaseId: releaseId,
            AcoustIdFingerprint: null,
            CoverArt: coverArt
        );

    // ── Field round-trips ─────────────────────────────────────────────────

    [Fact]
    public async Task WriteTagsAsync_Title_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(title: "My Song"),
            tag => tag.Title.Should().Be("My Song")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_Artist_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(artist: "David Bowie"),
            tag => tag.FirstPerformer.Should().Be("David Bowie")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_AlbumArtist_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(albumArtist: "Various Artists"),
            tag => tag.FirstAlbumArtist.Should().Be("Various Artists")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_Album_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(album: "Ziggy Stardust"),
            tag => tag.Album.Should().Be("Ziggy Stardust")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_TrackNumber_RoundTrips()
    {
        await WriteAndReopenAsync(BasicMetadata(trackNumber: 7), tag => tag.Track.Should().Be(7));
    }

    [Fact]
    public async Task WriteTagsAsync_DiscNumber_RoundTrips()
    {
        await WriteAndReopenAsync(BasicMetadata(discNumber: 2), tag => tag.Disc.Should().Be(2));
    }

    [Fact]
    public async Task WriteTagsAsync_Year_RoundTrips()
    {
        await WriteAndReopenAsync(BasicMetadata(year: 1972), tag => tag.Year.Should().Be(1972));
    }

    [Fact]
    public async Task WriteTagsAsync_Genre_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(genre: "Glam Rock"),
            tag => tag.FirstGenre.Should().Be("Glam Rock")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_MusicBrainzTrackId_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(trackId: "abc-123-def"),
            tag => tag.MusicBrainzTrackId.Should().Be("abc-123-def")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_MusicBrainzReleaseId_RoundTrips()
    {
        await WriteAndReopenAsync(
            BasicMetadata(releaseId: "rel-456-ghi"),
            tag => tag.MusicBrainzReleaseId.Should().Be("rel-456-ghi")
        );
    }

    // ── Cover art ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FromFile_EmbedsPicture()
    {
        // Write a 1x1 JPEG to a temp file and feed it as the cover source.
        byte[] tinyJpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS"
                + "Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ"
                + "CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy"
                + "MjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/"
                + "EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/"
                + "aAAwDAQACEQMRAD8AJQAB/9k="
        );
        string jpegPath = Path.Combine(Path.GetTempPath(), $"cover_{Guid.NewGuid():N}.jpg");
        await System.IO.File.WriteAllBytesAsync(jpegPath, tinyJpeg);

        try
        {
            AlbumArtSource source = new(FilePath: jpegPath, Url: null, Type: AlbumArtType.Front);
            await WriteAndReopenAsync(
                BasicMetadata(coverArt: source),
                tag => tag.Pictures.Should().HaveCount(1, "one picture should be embedded")
            );
        }
        finally
        {
            if (System.IO.File.Exists(jpegPath))
                System.IO.File.Delete(jpegPath);
        }
    }

    [Fact]
    public async Task WriteTagsAsync_NoCoverArt_DoesNotThrow()
    {
        Func<Task> act = () =>
            new TagLibAudioMetadataWriter().WriteTagsAsync(
                _tempFlac,
                BasicMetadata(coverArt: null),
                CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    // ── Null-safe edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task WriteTagsAsync_NullYear_SetsYearZero()
    {
        await WriteAndReopenAsync(BasicMetadata(year: null), tag => tag.Year.Should().Be(0));
    }

    [Fact]
    public async Task WriteTagsAsync_NullGenre_SetsEmptyGenres()
    {
        await WriteAndReopenAsync(BasicMetadata(genre: null), tag => tag.Genres.Should().BeEmpty());
    }
}
