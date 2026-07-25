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

using NoMercy.Encoder.Subtitles;

namespace NoMercy.Tests.Encoder.Subtitles;

/// <summary>
/// The no-op provider is the default registration so encoder-only test
/// contexts and dev setups without OpenSubtitles credentials still resolve
/// the dependency graph. Every search MUST yield an empty list and
/// downloads MUST return an empty byte array — never throw, never block,
/// never claim to be rate-limited.
/// </summary>
public class NoOpOpenSubtitlesProviderTests
{
    [Fact]
    public void IsRateLimited_AlwaysFalse()
    {
        NoOpOpenSubtitlesProvider provider = new();
        provider.IsRateLimited.Should().BeFalse();
    }

    [Fact]
    public async Task SearchByHashAsync_ReturnsEmpty()
    {
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByHashAsync(
            "abcdef1234567890",
            123456789L,
            ["eng", "fra"],
            CancellationToken.None
        );

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByFilenameAsync_ReturnsEmpty()
    {
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByFilenameAsync(
            "movie.mkv",
            ["eng"],
            CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_ReturnsEmpty()
    {
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByTitleAsync(
            "The Matrix",
            null,
            null,
            1999,
            ["eng"],
            CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_TvSeriesArgs_StillReturnsEmpty()
    {
        // TV variant carries season + episode — must not throw on either
        // ordering of nulls/values.
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByTitleAsync(
            "Breaking Bad",
            1,
            5,
            2008,
            ["eng", "spa"],
            CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ReturnsEmptyByteArray()
    {
        NoOpOpenSubtitlesProvider provider = new();

        byte[] payload = await provider.DownloadSubtitleAsync(
            "https://example.invalid/subtitle.srt.gz",
            CancellationToken.None
        );

        payload.Should().NotBeNull();
        payload.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByHashAsync_WithCancelledToken_StillReturnsEmpty()
    {
        // No-op provider doesn't observe cancellation — confirms the
        // implementation never throws OperationCanceledException, so
        // consumers can call it from cleanup paths safely.
        NoOpOpenSubtitlesProvider provider = new();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByHashAsync(
            "0",
            0,
            [],
            cts.Token
        );

        results.Should().BeEmpty();
    }
}
