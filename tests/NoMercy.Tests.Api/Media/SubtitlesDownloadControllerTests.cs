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
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Subtitles;
using NoMercy.Storage;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Exercises <c>POST subtitles/download</c> against the real <see cref="OpenSubtitlesAdapter"/> +
/// <see cref="NoMercy.MediaProcessing.Files.FileRepository"/> — only the raw XML-RPC
/// <see cref="IOpenSubtitlesProvider"/> and the <see cref="IStorageFactory"/> filesystem boundary
/// are mocked, so these tests prove the controller's write path end to end (convert → write
/// sidecar through the resolved storage → persist VideoFile.Subtitles), not a stand-in for it.
/// </summary>
[Trait("Category", "MediaSubtitles")]
public class SubtitlesDownloadControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    // Seeded in NoMercyApiFactory: Movie 129 (Spirited Away) has one VideoFile —
    // Filename "Spirited.Away.2001.1080p.mkv", HostFolder/Folder "/media/movies/Spirited
    // Away (2001)", Share = the movie folder's Ulid (matching production).
    private const int SeededMovieId = 129;

    public SubtitlesDownloadControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A MemoryStream whose Dispose() is a no-op so the test can inspect what the
    /// controller wrote to it after the "using" block inside the action has run.
    /// </summary>
    private sealed class NonDisposingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing) { }
    }

    /// <summary>
    /// The controller resolves the video's storage through <see cref="IStorageFactory"/>
    /// (VideoFile.Share is the folder's Ulid), so that is the seam the sidecar write
    /// crosses. Mocking <see cref="IStorageDriver"/> instead leaves the factory building
    /// a real local storage rooted at the seeded "/media/..." path, which does not exist
    /// on a test machine.
    /// </summary>
    private static Mock<IStorageFactory> MakeStorageFactoryMock(
        out NonDisposingMemoryStream sidecarStream,
        out Func<string?> capturedPath
    )
    {
        Mock<IStorage> storageMock = new();
        storageMock
            .Setup(s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                (string parent, string child) =>
                    $"{parent.TrimEnd('/', '\\')}/{child.TrimStart('/', '\\')}"
            );

        NonDisposingMemoryStream stream = new();
        string? path = null;
        storageMock
            .Setup(s => s.OpenWrite(It.IsAny<string>(), true))
            .Callback<string, bool>((p, _) => path = p)
            .Returns(stream);

        Mock<IStorageFactory> storageFactoryMock = new();
        storageFactoryMock
            .Setup(f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(storageMock.Object);

        sidecarStream = stream;
        capturedPath = () => path;
        return storageFactoryMock;
    }

    private HttpClient BuildClient(
        Mock<IOpenSubtitlesProvider> providerMock,
        Mock<IStorageFactory> storageFactoryMock
    )
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenSubtitlesProvider>();
                    services.AddSingleton(providerMock.Object);

                    services.RemoveAll<IStorageFactory>();
                    services.AddSingleton(storageFactoryMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    private static Mock<IOpenSubtitlesProvider> MakeProviderMock(byte[] downloadPayload)
    {
        Mock<IOpenSubtitlesProvider> provider = new();
        provider.Setup(p => p.IsRateLimited).Returns(false);
        provider
            .Setup(p =>
                p.DownloadSubtitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    priority: true
                )
            )
            .ReturnsAsync(downloadPayload);
        return provider;
    }

    private const string SampleSrt =
        "1\r\n00:00:01,000 --> 00:00:04,000\r\nHello, world!\r\n\r\n2\r\n00:00:05,500 --> 00:00:07,250\r\nSecond line.\r\n";

    // =========================================================================
    // Auth
    // =========================================================================

    [Fact]
    public async Task Download_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // Happy path — SRT candidate is converted, written through the storage
    // driver, and registered on VideoFile.Subtitles.
    // =========================================================================

    [Fact]
    public async Task Download_SrtCandidate_WritesVttSidecarThroughDriverAndReturnsTrackUrl()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            Encoding.UTF8.GetBytes(SampleSrt)
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream sidecarStream,
            out Func<string?> capturedPath
        );

        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        // The driver write happened, at a path directly beside the video file,
        // named to match VideoPlaylistResponseDto.Subtitles(VideoFile)'s URL formula.
        capturedPath()
            .Should()
            .Be(
                "/media/movies/Spirited Away (2001)/subtitlesSpirited.Away.2001.1080p.mkv.eng.full.vtt"
            );

        string writtenContent = Encoding.UTF8.GetString(sidecarStream.ToArray());
        writtenContent.Should().StartWith("WEBVTT");
        writtenContent.Should().Contain("00:00:01.000 --> 00:00:04.000");
        writtenContent.Should().NotContain(",000");

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetProperty("kind").GetString().Should().Be("subtitles");
        data.GetProperty("language").GetString().Should().Be("eng");
        data.GetProperty("label").GetString().Should().Be("full");
        data.GetProperty("file")
            .GetString()
            .Should()
            .EndWith("/subtitlesSpirited.Away.2001.1080p.mkv.eng.full.vtt");

        // VideoFile.Subtitles now carries the new entry — the next watch response
        // rebuilds `tracks` from this column, so the sub persists for future plays.
        using MediaContext context = new();
        VideoFile persisted = context.VideoFiles.Single(vf => vf.MovieId == SeededMovieId);
        List<JsonPropertyBag>? subtitles = JsonConvert.DeserializeObject<List<JsonPropertyBag>>(
            persisted.Subtitles ?? "[]"
        );

        subtitles.Should().NotBeNull();
        subtitles!
            .Should()
            .ContainSingle(s => s.Language == "eng" && s.Type == "full" && s.Ext == "vtt");
    }

    [Fact]
    public async Task Download_SameLanguageTwice_ReplacesRatherThanDuplicates()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            Encoding.UTF8.GetBytes(SampleSrt)
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream _,
            out Func<string?> _
        );

        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        object payload = new
        {
            type = "movie",
            id = SeededMovieId,
            download_url = "https://93.184.216.34/spirited-away.srt",
            language = "eng",
            format = "srt",
        };

        HttpResponseMessage first = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            payload
        );
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            payload
        );
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        using MediaContext context = new();
        VideoFile persisted = context.VideoFiles.Single(vf => vf.MovieId == SeededMovieId);
        List<JsonPropertyBag>? subtitles = JsonConvert.DeserializeObject<List<JsonPropertyBag>>(
            persisted.Subtitles ?? "[]"
        );

        subtitles.Should().NotBeNull();
        subtitles!.Count(s => s.Language == "eng" && s.Type == "full").Should().Be(1);
    }

    // =========================================================================
    // Rate limit -> 429, never a 500
    // =========================================================================

    [Fact]
    public async Task Download_ProviderRateLimited_Returns429NotServerError()
    {
        Mock<IOpenSubtitlesProvider> providerMock = new();
        providerMock.Setup(p => p.IsRateLimited).Returns(false);
        providerMock
            .Setup(p =>
                p.DownloadSubtitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    priority: true
                )
            )
            .ThrowsAsync(new OpenSubtitlesRateLimitException());
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream _,
            out Func<string?> _
        );

        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, body);
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // =========================================================================
    // Unsupported format -> 422, never written to disk
    // =========================================================================

    [Fact]
    public async Task Download_UnsupportedFormat_Returns422AndDoesNotWriteToStorage()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            Encoding.UTF8.GetBytes("[Script Info]\n; ASS content")
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream _,
            out Func<string?> capturedPath
        );

        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.ass",
                language = "eng",
                format = "ass",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        capturedPath()
            .Should()
            .BeNull("an unsupported format must be rejected before anything reaches storage");
    }

    // =========================================================================
    // Validation
    // =========================================================================

    [Fact]
    public async Task Download_InvalidType_ReturnsBadRequest()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock([]);
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream _,
            out Func<string?> _
        );
        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "album",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_MissingDownloadUrl_ReturnsBadRequest()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock([]);
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            out NonDisposingMemoryStream _,
            out Func<string?> _
        );
        HttpClient client = BuildClient(providerMock, storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/subtitles/download",
            new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "",
                language = "eng",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Minimal shape matching VideoPlaylistResponseDto.Subtitle's JSON contract.</summary>
    private sealed class JsonPropertyBag
    {
        [JsonProperty("language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("ext")]
        public string Ext { get; set; } = string.Empty;
    }
}
