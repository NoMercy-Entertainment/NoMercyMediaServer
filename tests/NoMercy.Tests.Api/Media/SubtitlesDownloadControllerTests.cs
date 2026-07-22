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
[Trait(name: "Category", value: "MediaSubtitles")]
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
            .Setup(expression: s => s.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                valueFunction: (string parent, string child) =>
                    $"{parent.TrimEnd(trimChars: ['/', '\\'])}/{child.TrimStart(trimChars: ['/', '\\'])}"
            );

        NonDisposingMemoryStream stream = new();
        string? path = null;
        storageMock
            .Setup(expression: s => s.OpenWrite(It.IsAny<string>(), true))
            .Callback<string, bool>(action: (p, _) => path = p)
            .Returns(value: stream);

        Mock<IStorageFactory> storageFactoryMock = new();
        storageFactoryMock
            .Setup(expression: f => f.For(It.IsAny<Ulid>(), It.IsAny<Ulid>(), It.IsAny<string>()))
            .Returns(value: storageMock.Object);

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
            .WithWebHostBuilder(configuration: builder =>
            {
                builder.ConfigureTestServices(servicesConfiguration: services =>
                {
                    services.RemoveAll<IOpenSubtitlesProvider>();
                    services.AddSingleton(implementationInstance: providerMock.Object);

                    services.RemoveAll<IStorageFactory>();
                    services.AddSingleton(implementationInstance: storageFactoryMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    private static Mock<IOpenSubtitlesProvider> MakeProviderMock(byte[] downloadPayload)
    {
        Mock<IOpenSubtitlesProvider> provider = new();
        provider.Setup(expression: p => p.IsRateLimited).Returns(value: false);
        provider
            .Setup(expression: p =>
                p.DownloadSubtitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    priority: true
                )
            )
            .ReturnsAsync(value: downloadPayload);
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
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        response.StatusCode.Should().BeOneOf(validValues: [HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden]);
    }

    // =========================================================================
    // Happy path — SRT candidate is converted, written through the storage
    // driver, and registered on VideoFile.Subtitles.
    // =========================================================================

    [Fact]
    public async Task Download_SrtCandidate_WritesVttSidecarThroughDriverAndReturnsTrackUrl()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            downloadPayload: Encoding.UTF8.GetBytes(s: SampleSrt)
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream sidecarStream,
            capturedPath: out Func<string?> capturedPath
        );

        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.OK, because: body);

        // The driver write happened, at a path directly beside the video file,
        // named to match VideoPlaylistResponseDto.Subtitles(VideoFile)'s URL formula.
        capturedPath()
            .Should()
            .Be(
                expected: "/media/movies/Spirited Away (2001)/subtitlesSpirited.Away.2001.1080p.mkv.eng.full.vtt"
            );

        string writtenContent = Encoding.UTF8.GetString(bytes: sidecarStream.ToArray());
        writtenContent.Should().StartWith(expected: "WEBVTT");
        writtenContent.Should().Contain(expected: "00:00:01.000 --> 00:00:04.000");
        writtenContent.Should().NotContain(unexpected: ",000");

        using JsonDocument doc = JsonDocument.Parse(json: body);
        JsonElement data = doc.RootElement.GetProperty(propertyName: "data");
        data.GetProperty(propertyName: "kind").GetString().Should().Be(expected: "subtitles");
        data.GetProperty(propertyName: "language").GetString().Should().Be(expected: "eng");
        data.GetProperty(propertyName: "label").GetString().Should().Be(expected: "full");
        data.GetProperty(propertyName: "file")
            .GetString()
            .Should()
            .EndWith(expected: "/subtitlesSpirited.Away.2001.1080p.mkv.eng.full.vtt");

        // VideoFile.Subtitles now carries the new entry — the next watch response
        // rebuilds `tracks` from this column, so the sub persists for future plays.
        using MediaContext context = new();
        VideoFile persisted = context.VideoFiles.Single(predicate: vf => vf.MovieId == SeededMovieId);
        List<JsonPropertyBag>? subtitles = JsonConvert.DeserializeObject<List<JsonPropertyBag>>(
            value: persisted.Subtitles ?? "[]"
        );

        subtitles.Should().NotBeNull();
        subtitles!
            .Should()
            .ContainSingle(predicate: s => s.Language == "eng" && s.Type == "full" && s.Ext == "vtt");
    }

    [Fact]
    public async Task Download_SameLanguageTwice_ReplacesRatherThanDuplicates()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            downloadPayload: Encoding.UTF8.GetBytes(s: SampleSrt)
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream _,
            capturedPath: out Func<string?> _
        );

        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        object payload = new
        {
            type = "movie",
            id = SeededMovieId,
            download_url = "https://93.184.216.34/spirited-away.srt",
            language = "eng",
            format = "srt",
        };

        HttpResponseMessage first = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: payload
        );
        first.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        HttpResponseMessage second = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: payload
        );
        second.StatusCode.Should().Be(expected: HttpStatusCode.OK);

        using MediaContext context = new();
        VideoFile persisted = context.VideoFiles.Single(predicate: vf => vf.MovieId == SeededMovieId);
        List<JsonPropertyBag>? subtitles = JsonConvert.DeserializeObject<List<JsonPropertyBag>>(
            value: persisted.Subtitles ?? "[]"
        );

        subtitles.Should().NotBeNull();
        subtitles!.Count(predicate: s => s is { Language: "eng", Type: "full" }).Should().Be(expected: 1);
    }

    // =========================================================================
    // Rate limit -> 429, never a 500
    // =========================================================================

    [Fact]
    public async Task Download_ProviderRateLimited_Returns429NotServerError()
    {
        Mock<IOpenSubtitlesProvider> providerMock = new();
        providerMock.Setup(expression: p => p.IsRateLimited).Returns(value: false);
        providerMock
            .Setup(expression: p =>
                p.DownloadSubtitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    priority: true
                )
            )
            .ThrowsAsync(exception: new OpenSubtitlesRateLimitException());
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream _,
            capturedPath: out Func<string?> _
        );

        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
                format = "srt",
            }
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(expected: HttpStatusCode.TooManyRequests, because: body);
        response.StatusCode.Should().NotBe(unexpected: HttpStatusCode.InternalServerError);
    }

    // =========================================================================
    // Unsupported format -> 422, never written to disk
    // =========================================================================

    [Fact]
    public async Task Download_UnsupportedFormat_Returns422AndDoesNotWriteToStorage()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(
            downloadPayload: Encoding.UTF8.GetBytes(s: "[Script Info]\n; ASS content")
        );
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream _,
            capturedPath: out Func<string?> capturedPath
        );

        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.ass",
                language = "eng",
                format = "ass",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.UnprocessableEntity);
        capturedPath()
            .Should()
            .BeNull(because: "an unsupported format must be rejected before anything reaches storage");
    }

    // =========================================================================
    // Validation
    // =========================================================================

    [Fact]
    public async Task Download_InvalidType_ReturnsBadRequest()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(downloadPayload: []);
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream _,
            capturedPath: out Func<string?> _
        );
        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "album",
                id = SeededMovieId,
                download_url = "https://93.184.216.34/spirited-away.srt",
                language = "eng",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_MissingDownloadUrl_ReturnsBadRequest()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock(downloadPayload: []);
        Mock<IStorageFactory> storageFactoryMock = MakeStorageFactoryMock(
            sidecarStream: out NonDisposingMemoryStream _,
            capturedPath: out Func<string?> _
        );
        HttpClient client = BuildClient(providerMock: providerMock, storageFactoryMock: storageFactoryMock);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            requestUri: "/api/v1/subtitles/download",
            value: new
            {
                type = "movie",
                id = SeededMovieId,
                download_url = "",
                language = "eng",
            }
        );

        response.StatusCode.Should().Be(expected: HttpStatusCode.BadRequest);
    }

    /// <summary>Minimal shape matching VideoPlaylistResponseDto.Subtitle's JSON contract.</summary>
    private sealed class JsonPropertyBag
    {
        [JsonProperty(propertyName: "language")]
        public string Language { get; set; } = string.Empty;

        [JsonProperty(propertyName: "type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty(propertyName: "ext")]
        public string Ext { get; set; } = string.Empty;
    }
}
