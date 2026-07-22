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

using Microsoft.Extensions.DependencyInjection;
using Moq;
using NoMercy.Api.Services.Music;
using NoMercy.Data.Repositories;
using NoMercy.Database.Models.Music;
using NoMercy.Providers.Abstractions;
using NoMercy.Providers.Lyrics;
using Xunit;

namespace NoMercy.Tests.Api.Services.Music;

/// <summary>
/// Covers the negative-cache classification added alongside the lyrics resolve
/// perf work: a confirmed "no lyrics anywhere" must persist the permanent "[]"
/// marker (so later plays never re-hit Lrclib/Musixmatch), but a transient
/// provider failure (timeout / rate limit / unexpected error) must NOT --
/// otherwise every hiccup would permanently blacklist a track that was simply
/// never checked. A short in-memory backoff still stops the very next play
/// from immediately re-running the same slow chain.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class LyricsResolverTests
{
    private static Track MakeUncachedTrack() => new() { Id = Guid.NewGuid(), Name = "Track" };

    private static (
        LyricsResolver Resolver,
        Mock<IMusicRepository> Repository,
        Mock<ILyricsAggregator> Aggregator
    ) MakeResolver(Track track, TimeSpan? transientBackoff = null)
    {
        Mock<IMusicRepository> repository = new();
        repository
            .Setup(expression: r => r.GetTrackWithIncludesAsync(track.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: track);

        Mock<ILyricsAggregator> aggregator = new();

        ServiceCollection services = new();
        services.AddSingleton(implementationInstance: repository.Object);
        ServiceProvider provider = services.BuildServiceProvider();

        LyricsResolver resolver = transientBackoff is { } backoff
            ? new(scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(), lyricsAggregator: aggregator.Object, transientBackoff: backoff)
            : new(scopeFactory: provider.GetRequiredService<IServiceScopeFactory>(), lyricsAggregator: aggregator.Object);

        return (resolver, repository, aggregator);
    }

    [Fact]
    public async Task ResolveAsync_DefinitiveMiss_WritesPermanentEmptyMarker()
    {
        Track track = MakeUncachedTrack();
        (
            LyricsResolver resolver,
            Mock<IMusicRepository> repository,
            Mock<ILyricsAggregator> aggregator
        ) = MakeResolver(track: track);
        aggregator.Setup(expression: a => a.SearchLyrics(track)).ReturnsAsync(value: LyricsFetchResult.NotFound);

        Lyric[]? result = await resolver.ResolveAsync(trackId: track.Id);

        result.Should().BeNull();
        repository.Verify(
            expression: r => r.UpdateTrackLyricsAsync(track, "[]", It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task ResolveAsync_TransientError_DoesNotWritePermanentMarker()
    {
        Track track = MakeUncachedTrack();
        (
            LyricsResolver resolver,
            Mock<IMusicRepository> repository,
            Mock<ILyricsAggregator> aggregator
        ) = MakeResolver(track: track);
        aggregator
            .Setup(expression: a => a.SearchLyrics(track))
            .ReturnsAsync(value: LyricsFetchResult.TransientFailure);

        Lyric[]? result = await resolver.ResolveAsync(trackId: track.Id);

        result.Should().BeNull();
        repository.Verify(
            expression: r =>
                r.UpdateTrackLyricsAsync(
                    It.IsAny<Track>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task ResolveAsync_TransientError_BacksOffInsteadOfRetryingImmediately()
    {
        Track track = MakeUncachedTrack();
        (LyricsResolver resolver, _, Mock<ILyricsAggregator> aggregator) = MakeResolver(
            track: track,
            transientBackoff: TimeSpan.FromMinutes(minutes: 2)
        );
        aggregator
            .Setup(expression: a => a.SearchLyrics(track))
            .ReturnsAsync(value: LyricsFetchResult.TransientFailure);

        await resolver.ResolveAsync(trackId: track.Id);
        await resolver.ResolveAsync(trackId: track.Id);

        // The second call landed inside the backoff window, so it must never
        // touch the aggregator (and therefore never re-hit the rate-limited
        // providers) a second time.
        aggregator.Verify(expression: a => a.SearchLyrics(track), times: Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_TransientError_RetriesForRealOnceBackoffExpires()
    {
        Track track = MakeUncachedTrack();
        (LyricsResolver resolver, _, Mock<ILyricsAggregator> aggregator) = MakeResolver(
            track: track,
            transientBackoff: TimeSpan.FromMilliseconds(milliseconds: 20)
        );
        aggregator
            .Setup(expression: a => a.SearchLyrics(track))
            .ReturnsAsync(value: LyricsFetchResult.TransientFailure);

        await resolver.ResolveAsync(trackId: track.Id);
        await Task.Delay(delay: TimeSpan.FromMilliseconds(milliseconds: 100));
        await resolver.ResolveAsync(trackId: track.Id);

        aggregator.Verify(expression: a => a.SearchLyrics(track), times: Times.Exactly(callCount: 2));
    }

    [Fact]
    public async Task ResolveAsync_Found_PersistsSerializedLines()
    {
        Track track = MakeUncachedTrack();
        (
            LyricsResolver resolver,
            Mock<IMusicRepository> repository,
            Mock<ILyricsAggregator> aggregator
        ) = MakeResolver(track: track);
        LyricLine[] lines = [new() { Text = "line one" }];
        Lyric[] persisted = [new() { Text = "line one" }];
        aggregator
            .Setup(expression: a => a.SearchLyrics(track))
            .ReturnsAsync(value: LyricsFetchResult.Found(lines: lines, winner: "Lrclib-get"));
        repository
            .Setup(expression: r =>
                r.UpdateTrackLyricsAsync(
                    track,
                    It.Is<string>(json => json.Contains("line one")),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: persisted);

        Lyric[]? result = await resolver.ResolveAsync(trackId: track.Id);

        result.Should().BeEquivalentTo(expectation: persisted);
    }
}
