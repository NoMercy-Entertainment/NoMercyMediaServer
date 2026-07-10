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
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NoMercy.Encoder.Subtitles;
using NoMercy.Tests.Api.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Api.Media;

/// <summary>
/// Exercises SubtitlesController against the real <see cref="OpenSubtitlesAdapter"/> — only
/// the raw XML-RPC <see cref="IOpenSubtitlesProvider"/> is mocked, so these tests prove the
/// controller + adapter wiring, not a stand-in for it.
/// </summary>
[Trait("Category", "MediaSubtitles")]
public class SubtitlesControllerTests : IClassFixture<NoMercyApiFactory>
{
    private readonly NoMercyApiFactory _factory;

    // Seeded in NoMercyApiFactory: Movie 129 (Spirited Away) has one VideoFile;
    // Tv 1399 (Breaking Bad) has Episode 62085 (S01E01) with one VideoFile. Neither
    // seeded VideoFile path exists on the test machine's disk, so every test here
    // naturally exercises the hash-unavailable fallback path.
    private const int SeededMovieId = 129;
    private const int SeededTvShowId = 1399;

    public SubtitlesControllerTests(NoMercyApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient BuildClient(Mock<IOpenSubtitlesProvider> providerMock)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IOpenSubtitlesProvider>();
                    services.AddSingleton(providerMock.Object);
                });
            })
            .CreateClient()
            .AsAuthenticated();
    }

    private static Mock<IOpenSubtitlesProvider> MakeProviderMock()
    {
        Mock<IOpenSubtitlesProvider> provider = new();
        provider.Setup(p => p.IsRateLimited).Returns(false);
        return provider;
    }

    // =========================================================================
    // Auth
    // =========================================================================

    [Fact]
    public async Task Search_Unauthenticated_ReturnsUnauthorized()
    {
        HttpClient client = _factory.CreateClient().AsUnauthenticated();

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/subtitles/search?type=movie&id={SeededMovieId}"
        );

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // =========================================================================
    // Happy path — provider candidates are mapped and wrapped in the envelope
    // =========================================================================

    [Fact]
    public async Task Search_Movie_MapsProviderCandidatesIntoEnvelope()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock();
        providerMock
            .Setup(p =>
                p.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new(
                    Language: "eng",
                    SubRating: "9.0",
                    SubDownloadsCnt: "4321",
                    SubFromTrusted: "1",
                    MovieFPS: "23.976",
                    SubDownloadLink: "https://dl.example.com/spirited-away.srt",
                    SubFormat: "srt",
                    MatchedBy: "tag",
                    SubFileName: "Spirited.Away.2001.1080p.srt",
                    MovieReleaseName: "Spirited.Away.2001.1080p.BluRay",
                    SubHearingImpaired: "0",
                    UserNickName: "SubUploader"
                ),
            ]);

        HttpClient client = BuildClient(providerMock);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/subtitles/search?type=movie&id={SeededMovieId}&language=eng"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);

        JsonElement first = data[0];
        first.GetProperty("language").GetString().Should().Be("eng");
        first
            .GetProperty("download_url")
            .GetString()
            .Should()
            .Be("https://dl.example.com/spirited-away.srt");
        first.GetProperty("id").GetString().Should().Be("https://dl.example.com/spirited-away.srt");
        first.GetProperty("downloads").GetInt32().Should().Be(4321);
        first.GetProperty("rating").GetDouble().Should().BeApproximately(9.0, 0.001);
        first.GetProperty("format").GetString().Should().Be("srt");
        first.GetProperty("trusted").GetBoolean().Should().BeTrue();
        first.GetProperty("hearing_impaired").GetBoolean().Should().BeFalse();
        first
            .GetProperty("release_name")
            .GetString()
            .Should()
            .Be("Spirited.Away.2001.1080p.BluRay");
        first.GetProperty("uploader").GetString().Should().Be("SubUploader");

        // Hash search must have been skipped — the seeded VideoFile path is not on disk.
        providerMock.Verify(
            p =>
                p.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    // =========================================================================
    // Hash unavailable -> filename empty -> title fallback, without throwing
    // =========================================================================

    [Fact]
    public async Task Search_Tv_FallsBackToTitleSearch_WhenHashAndFilenameFindNothing()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock();
        providerMock
            .Setup(p =>
                p.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        providerMock
            .Setup(p =>
                p.SearchByTitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([
                new(
                    Language: "eng",
                    SubRating: "7.5",
                    SubDownloadsCnt: "10",
                    SubFromTrusted: "0",
                    MovieFPS: null,
                    SubDownloadLink: "https://dl.example.com/breaking-bad-s01e01.srt",
                    SubFormat: "srt",
                    MatchedBy: "title"
                ),
            ]);

        HttpClient client = BuildClient(providerMock);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/subtitles/search?type=tv&id={SeededTvShowId}&language=eng"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement data = doc.RootElement.GetProperty("data");
        data.GetArrayLength().Should().Be(1);
        data[0]
            .GetProperty("download_url")
            .GetString()
            .Should()
            .Be("https://dl.example.com/breaking-bad-s01e01.srt");

        // Title search must have been reached with the resolved season/episode —
        // proving the hash-unavailable -> empty-filename -> title chain completed
        // without throwing.
        providerMock.Verify(
            p =>
                p.SearchByTitleAsync(
                    It.IsAny<string>(),
                    1,
                    1,
                    It.IsAny<int?>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    // =========================================================================
    // Rate limit -> 429, never a 500
    // =========================================================================

    [Fact]
    public async Task Search_ProviderRateLimited_Returns429NotServerError()
    {
        Mock<IOpenSubtitlesProvider> providerMock = new();
        bool rateLimited = false;
        providerMock.Setup(p => p.IsRateLimited).Returns(() => rateLimited);

        providerMock
            .Setup(p =>
                p.SearchByFilenameAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => rateLimited = true)
            .ThrowsAsync(new OpenSubtitlesRateLimitException());

        providerMock
            .Setup(p =>
                p.SearchByTitleAsync(
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);

        HttpClient client = BuildClient(providerMock);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/subtitles/search?type=movie&id={SeededMovieId}&language=eng"
        );

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, body);
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // =========================================================================
    // Validation
    // =========================================================================

    [Fact]
    public async Task Search_InvalidType_ReturnsBadRequest()
    {
        Mock<IOpenSubtitlesProvider> providerMock = MakeProviderMock();
        HttpClient client = BuildClient(providerMock);

        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/subtitles/search?type=album&id={SeededMovieId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
