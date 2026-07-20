// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Text;
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

    // ── Cover art from URL — real loopback HTTP server, no HttpClient mock ─
    //
    // ResolveCoverBytesAsync constructs its own `new HttpClient()` inline
    // with no DI seam, so per the "real input surface, not a mock of the
    // unit" rule this exercises a genuine socket round-trip against a
    // minimal hand-rolled HTTP/1.1 server on loopback rather than mocking
    // HttpClient itself.

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FromUrl_DownloadsAndEmbedsPicture()
    {
        byte[] tinyJpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS"
                + "Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ"
                + "CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy"
                + "MjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/"
                + "EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/"
                + "aAAwDAQACEQMRAD8AJQAB/9k="
        );

        await using LoopbackHttpServer server = await LoopbackHttpServer.StartAsync(tinyJpeg);

        AlbumArtSource source = new(
            FilePath: null,
            Url: $"http://127.0.0.1:{server.Port}/cover.jpg",
            Type: AlbumArtType.Front
        );
        await WriteAndReopenAsync(
            BasicMetadata(coverArt: source),
            tag => tag.Pictures.Should().HaveCount(1, "the downloaded cover must be embedded")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FromUrl_ConnectionRefused_DoesNotThrowAndEmbedsNoPicture()
    {
        // Port 1 on loopback refuses immediately on every platform this
        // suite runs on — a deterministic, real (unmocked) network failure.
        AlbumArtSource source = new(
            FilePath: null,
            Url: "http://127.0.0.1:1/cover.jpg",
            Type: AlbumArtType.Front
        );

        await WriteAndReopenAsync(
            BasicMetadata(coverArt: source),
            tag => tag.Pictures.Should().BeEmpty("a failed cover download must degrade, not throw")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FilePathSetButMissing_AndNoUrl_EmbedsNoPicture()
    {
        AlbumArtSource source = new(
            FilePath: Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.jpg"),
            Url: null,
            Type: AlbumArtType.Front
        );

        await WriteAndReopenAsync(
            BasicMetadata(coverArt: source),
            tag => tag.Pictures.Should().BeEmpty()
        );
    }

    /// <summary>
    /// Minimal single-request HTTP/1.1 server over a raw <see cref="TcpListener"/>
    /// on loopback — avoids HttpListener's Windows URL-ACL reservation
    /// requirement while still exercising a real socket round trip.
    /// </summary>
    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _acceptLoop;
        private readonly CancellationTokenSource _cts = new();

        public int Port { get; }

        private LoopbackHttpServer(TcpListener listener, byte[] body)
        {
            _listener = listener;
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptLoop = AcceptOnceAsync(body, _cts.Token);
        }

        public static Task<LoopbackHttpServer> StartAsync(byte[] responseBody)
        {
            TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new LoopbackHttpServer(listener, responseBody));
        }

        private async Task AcceptOnceAsync(byte[] body, CancellationToken ct)
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                await using NetworkStream stream = client.GetStream();

                // Drain the request (headers terminated by CRLFCRLF) without
                // parsing it — this server always answers the same fixed body.
                byte[] buffer = new byte[4096];
                int total = 0;
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(total), ct);
                    if (read == 0)
                        break;
                    total += read;
                    string soFar = Encoding.ASCII.GetString(buffer, 0, total);
                    if (soFar.Contains("\r\n\r\n", StringComparison.Ordinal))
                        break;
                }

                string headers =
                    "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: image/jpeg\r\n"
                    + $"Content-Length: {body.Length}\r\n"
                    + "Connection: close\r\n"
                    + "\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
                await stream.WriteAsync(headerBytes, ct);
                await stream.WriteAsync(body, ct);
                await stream.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Server disposed before a request arrived — fine for tests
                // that don't expect a call.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch
            {
                // best-effort shutdown
            }
        }
    }
}
