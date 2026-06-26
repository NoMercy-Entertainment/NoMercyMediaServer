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

using Moq;
using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

public class MovieHashAlgorithmTests
{
    // Known fixture: 64KB + 1 byte of 0x00 with file size = 65537 (0x10001).
    // hash starts as fileSize=65537, then XOR's 8-byte chunks from first 64KB and last 64KB.
    // Both 64KB windows are all-zero so XOR contributes nothing — hash = fileSize = 0x10001 = 65537.
    // But the algorithm sums (+=), not XOR, so: hash = 65537 + (8192 * 0) + (8192 * 0) = 65537.
    [Fact]
    public void ComputeMovieHash_AllZeroFile_EqualsFileSize()
    {
        byte[] bytes = new byte[65537]; // 64KB + 1 byte, all zeros
        using MemoryStream stream = new(bytes);

        ulong hash = MovieHashHelper.ComputeMovieHash(stream, bytes.Length);

        // fileSize + sum of all 0-value chunks = fileSize
        hash.Should().Be((ulong)bytes.Length);
    }

    [Fact]
    public void ComputeMovieHash_SmallFile_UsesOverlappingWindows()
    {
        // File smaller than 64KB — both head and tail point to same data
        byte[] bytes = new byte[1024];
        // Set first 8 bytes to 1 so we can verify the contribution
        bytes[0] = 1;
        using MemoryStream stream = new(bytes);

        ulong hash = MovieHashHelper.ComputeMovieHash(stream, bytes.Length);

        // With a 1024-byte file: fileSize=1024, head window sum = 1 (first chunk), tail window sum = 1 (same data)
        // hash = 1024 + 1 + 1 = 1026
        hash.Should().Be(1026UL);
    }

    [Fact]
    public void FormatMovieHash_ReturnsLowercase16Chars()
    {
        ulong hash = 0x8e245d9679d31e12UL;
        string formatted = MovieHashHelper.FormatHash(hash);

        formatted.Should().Be("8e245d9679d31e12");
        formatted.Should().HaveLength(16);
        formatted.Should().MatchRegex("^[0-9a-f]{16}$");
    }

    [Fact]
    public void FormatMovieHash_ZeroHash_ProducesAllZeros()
    {
        string formatted = MovieHashHelper.FormatHash(0UL);
        formatted.Should().Be("0000000000000000");
    }
}

public class OpenSubtitlesAdapterTests
{
    private readonly Mock<IOpenSubtitlesProvider> _provider = new();
    private readonly OpenSubtitlesAdapter _adapter;

    public OpenSubtitlesAdapterTests()
    {
        _adapter = new OpenSubtitlesAdapter(_provider.Object);
    }

    [Fact]
    public async Task SearchByHash_TranslatesCandidatesCorrectly()
    {
        List<OpenSubtitlesSearchResult> providerResults =
        [
            new OpenSubtitlesSearchResult(
                Language: "en",
                SubRating: "8.5",
                SubDownloadsCnt: "1234",
                SubFromTrusted: "1",
                MovieFPS: "23.976",
                SubDownloadLink: "https://dl.example.com/sub1.srt",
                SubFormat: "srt",
                MatchedBy: "moviehash"
            ),
        ];

        _provider
            .Setup(p =>
                p.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(providerResults);

        IReadOnlyList<SubtitleCandidate> candidates = await _adapter.SearchByHashAsync(
            "8e245d9679d31e12",
            1234567890L,
            ["en"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None
        );

        candidates.Should().HaveCount(1);
        SubtitleCandidate c = candidates[0];
        c.Language.Should().Be("en");
        c.Rating.Should().BeApproximately(8.5, 0.001);
        c.Downloads.Should().Be(1234);
        c.IsTrustedUploader.Should().BeTrue();
        c.Fps.Should().BeApproximately(23.976, 0.001);
        c.DownloadUrl.Should().Be("https://dl.example.com/sub1.srt");
        c.Format.Should().Be("srt");
        c.Provider.Should().Be("OpenSubtitles");
    }

    [Fact]
    public async Task SearchByHash_TrustedUploadersOnly_FiltersUntrusted()
    {
        List<OpenSubtitlesSearchResult> providerResults =
        [
            new OpenSubtitlesSearchResult(
                Language: "en",
                SubRating: "7.0",
                SubDownloadsCnt: "500",
                SubFromTrusted: "0",
                MovieFPS: null,
                SubDownloadLink: "https://dl.example.com/sub2.srt",
                SubFormat: "srt",
                MatchedBy: "moviehash"
            ),
            new OpenSubtitlesSearchResult(
                Language: "en",
                SubRating: "6.0",
                SubDownloadsCnt: "200",
                SubFromTrusted: "1",
                MovieFPS: null,
                SubDownloadLink: "https://dl.example.com/sub3.srt",
                SubFormat: "srt",
                MatchedBy: "moviehash"
            ),
        ];

        _provider
            .Setup(p =>
                p.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(providerResults);

        IReadOnlyList<SubtitleCandidate> candidates = await _adapter.SearchByHashAsync(
            "abc",
            1000L,
            ["en"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None,
            trustedOnly: true
        );

        candidates.Should().HaveCount(1);
        candidates[0].IsTrustedUploader.Should().BeTrue();
    }

    [Fact]
    public async Task SearchByHash_RateLimited_ReturnsEmpty()
    {
        _provider
            .Setup(p =>
                p.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenSubtitlesRateLimitException());

        IReadOnlyList<SubtitleCandidate> candidates = await _adapter.SearchByHashAsync(
            "abc",
            1000L,
            ["en"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None
        );

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByHash_ReturnShape_IncludesLanguageRatingDownloadsFpsUrl()
    {
        List<OpenSubtitlesSearchResult> providerResults =
        [
            new OpenSubtitlesSearchResult(
                Language: "nl",
                SubRating: "5.0",
                SubDownloadsCnt: "99",
                SubFromTrusted: "0",
                MovieFPS: "25.0",
                SubDownloadLink: "https://dl.example.com/sub.vtt",
                SubFormat: "vtt",
                MatchedBy: "moviehash"
            ),
        ];

        _provider
            .Setup(p =>
                p.SearchByHashAsync(
                    It.IsAny<string>(),
                    It.IsAny<long>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(providerResults);

        IReadOnlyList<SubtitleCandidate> candidates = await _adapter.SearchByHashAsync(
            "def",
            2000L,
            ["nl"],
            TimeSpan.FromSeconds(5),
            CancellationToken.None
        );

        candidates.Should().HaveCount(1);
        SubtitleCandidate c = candidates[0];
        c.Language.Should().Be("nl");
        c.Rating.Should().Be(5.0);
        c.Downloads.Should().Be(99);
        c.Fps.Should().Be(25.0);
        c.DownloadUrl.Should().Be("https://dl.example.com/sub.vtt");
        c.Format.Should().Be("vtt");
    }
}
