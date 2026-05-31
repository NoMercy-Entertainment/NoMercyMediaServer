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
            movieHash: "abcdef1234567890",
            fileSize: 123456789L,
            languages: ["eng", "fra"],
            ct: CancellationToken.None
        );

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByFilenameAsync_ReturnsEmpty()
    {
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByFilenameAsync(
            filename: "movie.mkv",
            languages: ["eng"],
            ct: CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchByTitleAsync_ReturnsEmpty()
    {
        NoOpOpenSubtitlesProvider provider = new();

        IReadOnlyList<OpenSubtitlesSearchResult> results = await provider.SearchByTitleAsync(
            title: "The Matrix",
            season: null,
            episode: null,
            year: 1999,
            languages: ["eng"],
            ct: CancellationToken.None
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
            title: "Breaking Bad",
            season: 1,
            episode: 5,
            year: 2008,
            languages: ["eng", "spa"],
            ct: CancellationToken.None
        );

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadSubtitleAsync_ReturnsEmptyByteArray()
    {
        NoOpOpenSubtitlesProvider provider = new();

        byte[] payload = await provider.DownloadSubtitleAsync(
            downloadUrl: "https://example.invalid/subtitle.srt.gz",
            ct: CancellationToken.None
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
            movieHash: "0",
            fileSize: 0,
            languages: [],
            ct: cts.Token
        );

        results.Should().BeEmpty();
    }
}
