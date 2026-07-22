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
[Trait(name: "Category", value: "Unit")]
public class TagLibAudioMetadataWriterTests : IDisposable
{
    // Minimal valid FLAC: fLaC marker + STREAMINFO metadata block (34 bytes).
    // Bit-exact STREAMINFO: min/max blocksize=4096, min/max framesize=0,
    // sample_rate=44100, channels=2, bits=16, total_samples=0, md5=zeros.
    // 0x80 block-type byte = last-metadata-block (1) + type STREAMINFO (0).
    private static readonly byte[] MinimalFlacBytes = Convert.FromBase64String(
        s: "ZkxhQ4AAACIQABAAAAAAAAAACsRC8AAAAAAAAAAAAAAAAAAAAAAAAAAA"
    );

    private readonly string _tempFlac;

    public TagLibAudioMetadataWriterTests()
    {
        _tempFlac = Path.Combine(path1: Path.GetTempPath(), path2: $"test_{Guid.NewGuid():N}.flac");
        System.IO.File.WriteAllBytes(path: _tempFlac, bytes: MinimalFlacBytes);
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(path: _tempFlac))
            System.IO.File.Delete(path: _tempFlac);
        GC.SuppressFinalize(obj: this);
    }

    private async Task WriteAndReopenAsync(AudioMetadata metadata, Action<Tag> assertions)
    {
        TagLibAudioMetadataWriter writer = new();
        await writer.WriteTagsAsync(filePath: _tempFlac, metadata: metadata, ct: CancellationToken.None);

        using TagLib.File tagFile = TagLib.File.Create(path: _tempFlac);
        assertions(obj: tagFile.Tag);
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
            metadata: BasicMetadata(title: "My Song"),
            assertions: tag => tag.Title.Should().Be(expected: "My Song")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_Artist_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(artist: "David Bowie"),
            assertions: tag => tag.FirstPerformer.Should().Be(expected: "David Bowie")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_AlbumArtist_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(albumArtist: "Various Artists"),
            assertions: tag => tag.FirstAlbumArtist.Should().Be(expected: "Various Artists")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_Album_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(album: "Ziggy Stardust"),
            assertions: tag => tag.Album.Should().Be(expected: "Ziggy Stardust")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_TrackNumber_RoundTrips()
    {
        await WriteAndReopenAsync(metadata: BasicMetadata(trackNumber: 7), assertions: tag => tag.Track.Should().Be(expected: 7));
    }

    [Fact]
    public async Task WriteTagsAsync_DiscNumber_RoundTrips()
    {
        await WriteAndReopenAsync(metadata: BasicMetadata(discNumber: 2), assertions: tag => tag.Disc.Should().Be(expected: 2));
    }

    [Fact]
    public async Task WriteTagsAsync_Year_RoundTrips()
    {
        await WriteAndReopenAsync(metadata: BasicMetadata(year: 1972), assertions: tag => tag.Year.Should().Be(expected: 1972));
    }

    [Fact]
    public async Task WriteTagsAsync_Genre_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(genre: "Glam Rock"),
            assertions: tag => tag.FirstGenre.Should().Be(expected: "Glam Rock")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_MusicBrainzTrackId_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(trackId: "abc-123-def"),
            assertions: tag => tag.MusicBrainzTrackId.Should().Be(expected: "abc-123-def")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_MusicBrainzReleaseId_RoundTrips()
    {
        await WriteAndReopenAsync(
            metadata: BasicMetadata(releaseId: "rel-456-ghi"),
            assertions: tag => tag.MusicBrainzReleaseId.Should().Be(expected: "rel-456-ghi")
        );
    }

    // ── Cover art ─────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FromFile_EmbedsPicture()
    {
        // Write a 1x1 JPEG to a temp file and feed it as the cover source.
        byte[] tinyJpeg = Convert.FromBase64String(
            s: "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS"
               + "Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ"
               + "CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy"
               + "MjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/"
               + "EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/"
               + "aAAwDAQACEQMRAD8AJQAB/9k="
        );
        string jpegPath = Path.Combine(path1: Path.GetTempPath(), path2: $"cover_{Guid.NewGuid():N}.jpg");
        await System.IO.File.WriteAllBytesAsync(path: jpegPath, bytes: tinyJpeg);

        try
        {
            AlbumArtSource source = new(FilePath: jpegPath, Url: null, Type: AlbumArtType.Front);
            await WriteAndReopenAsync(
                metadata: BasicMetadata(coverArt: source),
                assertions: tag => tag.Pictures.Should().HaveCount(expected: 1, because: "one picture should be embedded")
            );
        }
        finally
        {
            if (System.IO.File.Exists(path: jpegPath))
                System.IO.File.Delete(path: jpegPath);
        }
    }

    [Fact]
    public async Task WriteTagsAsync_NoCoverArt_DoesNotThrow()
    {
        Func<Task> act = () =>
            new TagLibAudioMetadataWriter().WriteTagsAsync(
                filePath: _tempFlac,
                metadata: BasicMetadata(coverArt: null),
                ct: CancellationToken.None
            );

        await act.Should().NotThrowAsync();
    }

    // ── Null-safe edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task WriteTagsAsync_NullYear_SetsYearZero()
    {
        await WriteAndReopenAsync(metadata: BasicMetadata(year: null), assertions: tag => tag.Year.Should().Be(expected: 0));
    }

    [Fact]
    public async Task WriteTagsAsync_NullGenre_SetsEmptyGenres()
    {
        await WriteAndReopenAsync(metadata: BasicMetadata(genre: null), assertions: tag => tag.Genres.Should().BeEmpty());
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
            s: "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkS"
               + "Ew8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJ"
               + "CQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIy"
               + "MjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAFAABAAAAAAAAAAAAAAAAAAAACf/"
               + "EABQQAQAAAAAAAAAAAAAAAAAAAAD/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/"
               + "aAAwDAQACEQMRAD8AJQAB/9k="
        );

        await using LoopbackHttpServer server = await LoopbackHttpServer.StartAsync(responseBody: tinyJpeg);

        AlbumArtSource source = new(
            FilePath: null,
            Url: $"http://127.0.0.1:{server.Port}/cover.jpg",
            Type: AlbumArtType.Front
        );
        await WriteAndReopenAsync(
            metadata: BasicMetadata(coverArt: source),
            assertions: tag => tag.Pictures.Should().HaveCount(expected: 1, because: "the downloaded cover must be embedded")
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
            metadata: BasicMetadata(coverArt: source),
            assertions: tag => tag.Pictures.Should().BeEmpty(because: "a failed cover download must degrade, not throw")
        );
    }

    [Fact]
    public async Task WriteTagsAsync_CoverArt_FilePathSetButMissing_AndNoUrl_EmbedsNoPicture()
    {
        AlbumArtSource source = new(
            FilePath: Path.Combine(path1: Path.GetTempPath(), path2: $"missing_{Guid.NewGuid():N}.jpg"),
            Url: null,
            Type: AlbumArtType.Front
        );

        await WriteAndReopenAsync(
            metadata: BasicMetadata(coverArt: source),
            assertions: tag => tag.Pictures.Should().BeEmpty()
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
            _acceptLoop = AcceptOnceAsync(body: body, ct: _cts.Token);
        }

        public static Task<LoopbackHttpServer> StartAsync(byte[] responseBody)
        {
            TcpListener listener = new(localaddr: IPAddress.Loopback, port: 0);
            listener.Start();
            return Task.FromResult(result: new LoopbackHttpServer(listener: listener, body: responseBody));
        }

        private async Task AcceptOnceAsync(byte[] body, CancellationToken ct)
        {
            try
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken: ct);
                await using NetworkStream stream = client.GetStream();

                // Drain the request (headers terminated by CRLFCRLF) without
                // parsing it — this server always answers the same fixed body.
                byte[] buffer = new byte[4096];
                int total = 0;
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer: buffer.AsMemory(start: total), cancellationToken: ct);
                    if (read == 0)
                        break;
                    total += read;
                    string soFar = Encoding.ASCII.GetString(bytes: buffer, index: 0, count: total);
                    if (soFar.Contains(value: "\r\n\r\n", comparisonType: StringComparison.Ordinal))
                        break;
                }

                string headers =
                    "HTTP/1.1 200 OK\r\n"
                    + "Content-Type: image/jpeg\r\n"
                    + $"Content-Length: {body.Length}\r\n"
                    + "Connection: close\r\n"
                    + "\r\n";
                byte[] headerBytes = Encoding.ASCII.GetBytes(s: headers);
                await stream.WriteAsync(buffer: headerBytes, cancellationToken: ct);
                await stream.WriteAsync(buffer: body, cancellationToken: ct);
                await stream.FlushAsync(cancellationToken: ct);
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
