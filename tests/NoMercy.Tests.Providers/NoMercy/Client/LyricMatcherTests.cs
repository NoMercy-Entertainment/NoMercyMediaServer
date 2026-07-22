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

using NoMercy.Providers.Abstractions;
using NoMercy.Providers.NoMercy.Client;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Tests.Providers.NoMercy.Client;

/// <summary>
/// Tests the lyric match scoring that guards against the two reported failures:
/// lyrics from a completely different song, and synced lyrics from a different
/// release whose timing is off.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class LyricMatcherTests
{
    private static LyricLine[] SyncedLines() =>
        [
            new()
            {
                Text = "line one",
                Time = new() { Total = 1.0 },
            },
            new()
            {
                Text = "line two",
                Time = new() { Total = 5.0 },
            },
        ];

    private static LyricLine[] PlainLines() =>
        [
            new()
            {
                Text = "line one",
                Time = new() { Total = 0.0 },
            },
            new()
            {
                Text = "line two",
                Time = new() { Total = 0.0 },
            },
        ];

    private static LyricQuery Query(
        string title = "Bohemian Rhapsody",
        string artist = "Queen",
        int? duration = 354
    ) => new(Title: title, Artists: [artist], Album: "A Night at the Opera", DurationSeconds: duration);

    [Fact]
    public void Score_ExactMatch_IsAccepted()
    {
        LyricCandidate candidate = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 354, HasSyncedLyrics: true, Lines: SyncedLines());

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    [Fact]
    public void Score_DifferentSong_IsRejected()
    {
        LyricCandidate candidate = new(Title: "We Will Rock You", Artist: "Queen", DurationSeconds: 122, HasSyncedLyrics: true, Lines: SyncedLines());

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeLessThan(expected: 0);
    }

    [Fact]
    public void Score_DifferentArtist_IsRejected()
    {
        LyricCandidate candidate = new(
            Title: "Bohemian Rhapsody",
            Artist: "Panic! at the Disco",
            DurationSeconds: 354,
            HasSyncedLyrics: true,
            Lines: SyncedLines()
        );

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeLessThan(expected: 0);
    }

    [Fact]
    public void Score_SyncedWrongRelease_IsRejectedOnDuration()
    {
        // Same song and artist, but a live release 40s longer: synced timing would drift.
        LyricCandidate candidate = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 394, HasSyncedLyrics: true, Lines: SyncedLines());

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeLessThan(expected: 0);
    }

    [Fact]
    public void Score_SyncedWithinTolerance_IsAccepted()
    {
        LyricCandidate candidate = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 357, HasSyncedLyrics: true, Lines: SyncedLines());

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    [Fact]
    public void Score_PlainLyricsIgnoreDuration()
    {
        // Unsynced lyrics carry no timing, so a length difference must not reject them.
        LyricCandidate candidate = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 420, HasSyncedLyrics: false, Lines: PlainLines());

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    [Fact]
    public void Score_DecoratedTitle_StillMatches()
    {
        LyricCandidate candidate = new(
            Title: "Bohemian Rhapsody (Remastered 2011)",
            Artist: "Queen",
            DurationSeconds: 354,
            HasSyncedLyrics: true,
            Lines: SyncedLines()
        );

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    [Fact]
    public void PickBest_PrefersClosestDuration()
    {
        LyricCandidate near = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 355, HasSyncedLyrics: true, Lines: SyncedLines());
        LyricCandidate far = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 360, HasSyncedLyrics: true, Lines: SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(query: Query(), candidates: [far, near]);

        best.Should().BeSameAs(expected: near);
    }

    [Fact]
    public void PickBest_PrefersSyncedOverPlain()
    {
        LyricCandidate plain = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 354, HasSyncedLyrics: false, Lines: PlainLines());
        LyricCandidate synced = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 354, HasSyncedLyrics: true, Lines: SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(query: Query(), candidates: [plain, synced]);

        best.Should().BeSameAs(expected: synced);
    }

    [Fact]
    public void PickBest_AllMismatches_ReturnsNull()
    {
        LyricCandidate wrong = new(Title: "Some Other Song", Artist: "Another Band", DurationSeconds: 200, HasSyncedLyrics: true, Lines: SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(query: Query(), candidates: [wrong]);

        best.Should().BeNull();
    }

    [Fact]
    public void Score_EmptyLines_IsRejected()
    {
        LyricCandidate candidate = new(Title: "Bohemian Rhapsody", Artist: "Queen", DurationSeconds: 354, HasSyncedLyrics: true, Lines: []);

        double score = LyricMatcher.Score(query: Query(), candidate: candidate);

        score.Should().BeLessThan(expected: 0);
    }
}
