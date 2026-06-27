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
[Trait("Category", "Unit")]
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
    ) => new(title, [artist], "A Night at the Opera", duration);

    [Fact]
    public void Score_ExactMatch_IsAccepted()
    {
        LyricCandidate candidate = new("Bohemian Rhapsody", "Queen", 354, true, SyncedLines());

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Score_DifferentSong_IsRejected()
    {
        LyricCandidate candidate = new("We Will Rock You", "Queen", 122, true, SyncedLines());

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeLessThan(0);
    }

    [Fact]
    public void Score_DifferentArtist_IsRejected()
    {
        LyricCandidate candidate = new(
            "Bohemian Rhapsody",
            "Panic! at the Disco",
            354,
            true,
            SyncedLines()
        );

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeLessThan(0);
    }

    [Fact]
    public void Score_SyncedWrongRelease_IsRejectedOnDuration()
    {
        // Same song and artist, but a live release 40s longer: synced timing would drift.
        LyricCandidate candidate = new("Bohemian Rhapsody", "Queen", 394, true, SyncedLines());

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeLessThan(0);
    }

    [Fact]
    public void Score_SyncedWithinTolerance_IsAccepted()
    {
        LyricCandidate candidate = new("Bohemian Rhapsody", "Queen", 357, true, SyncedLines());

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Score_PlainLyricsIgnoreDuration()
    {
        // Unsynced lyrics carry no timing, so a length difference must not reject them.
        LyricCandidate candidate = new("Bohemian Rhapsody", "Queen", 420, false, PlainLines());

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Score_DecoratedTitle_StillMatches()
    {
        LyricCandidate candidate = new(
            "Bohemian Rhapsody (Remastered 2011)",
            "Queen",
            354,
            true,
            SyncedLines()
        );

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void PickBest_PrefersClosestDuration()
    {
        LyricCandidate near = new("Bohemian Rhapsody", "Queen", 355, true, SyncedLines());
        LyricCandidate far = new("Bohemian Rhapsody", "Queen", 360, true, SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(Query(), [far, near]);

        best.Should().BeSameAs(near);
    }

    [Fact]
    public void PickBest_PrefersSyncedOverPlain()
    {
        LyricCandidate plain = new("Bohemian Rhapsody", "Queen", 354, false, PlainLines());
        LyricCandidate synced = new("Bohemian Rhapsody", "Queen", 354, true, SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(Query(), [plain, synced]);

        best.Should().BeSameAs(synced);
    }

    [Fact]
    public void PickBest_AllMismatches_ReturnsNull()
    {
        LyricCandidate wrong = new("Some Other Song", "Another Band", 200, true, SyncedLines());

        LyricCandidate? best = LyricMatcher.PickBest(Query(), [wrong]);

        best.Should().BeNull();
    }

    [Fact]
    public void Score_EmptyLines_IsRejected()
    {
        LyricCandidate candidate = new("Bohemian Rhapsody", "Queen", 354, true, []);

        double score = LyricMatcher.Score(Query(), candidate);

        score.Should().BeLessThan(0);
    }
}
