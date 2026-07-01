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
using NoMercy.Providers.Lrclib.Client;
using NoMercy.Providers.Lrclib.Models;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Tests.Providers.Lrclib.Client;

[Trait("Category", "Unit")]
public class LrclibClientToCandidateTests
{
    [Fact]
    public void ToCandidate_WithInstrumental_ReturnsNull()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Symphony No. 9",
            ArtistName = "Beethoven",
            Duration = 300,
            Instrumental = true,
            SyncedLyrics = "[00:00.00]Instrumental",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithoutSyncedOrPlainLyrics_ReturnsNull()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 300,
            Instrumental = false,
            SyncedLyrics = "",
            PlainLyrics = "",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_PrefersSyncedOverPlain()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 200,
            Instrumental = false,
            SyncedLyrics = "[00:01.50]Synced line",
            PlainLyrics = "Plain line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().NotBeNull();
        candidate!.HasSyncedLyrics.Should().BeTrue();
    }

    [Fact]
    public void ToCandidate_UsesPlainWhenNoSynced()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 200,
            Instrumental = false,
            SyncedLyrics = "",
            PlainLyrics = "Plain line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().NotBeNull();
        candidate!.HasSyncedLyrics.Should().BeFalse();
    }

    [Fact]
    public void ToCandidate_RoundsPositiveDuration()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 200.6,
            Instrumental = false,
            SyncedLyrics = "[00:01.00]Line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.DurationSeconds.Should().Be(201);
    }

    [Fact]
    public void ToCandidate_RoundsDown()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 200.4,
            Instrumental = false,
            SyncedLyrics = "[00:01.00]Line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.DurationSeconds.Should().Be(200);
    }

    [Fact]
    public void ToCandidate_WithZeroDuration_ReturnsDurationNull()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song Name",
            ArtistName = "Artist Name",
            Duration = 0,
            Instrumental = false,
            SyncedLyrics = "[00:01.00]Line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_MapsTitleAndArtist()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Bohemian Rhapsody",
            ArtistName = "Queen",
            Duration = 354,
            Instrumental = false,
            SyncedLyrics = "[00:01.00]Line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Title.Should().Be("Bohemian Rhapsody");
        candidate.Artist.Should().Be("Queen");
    }

    [Fact]
    public void ToCandidate_ParsesSyncedLyrics()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = "[00:01.50]First\n[00:03.20]Second",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("First");
        candidate.Lines[0].Time.Total.Should().Be(1.5);
        candidate.Lines[1].Text.Should().Be("Second");
        candidate.Lines[1].Time.Total.Should().Be(3.2);
    }

    [Fact]
    public void ToCandidate_FiltersBlanklinesFromLyrics()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = "[00:01.00]First\n\n[00:02.00]Second",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("First");
        candidate.Lines[1].Text.Should().Be("Second");
    }

    [Fact]
    public void ToCandidate_WithOnlyBlankLines_ReturnsNull()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = "\n\n\n",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_ParsesUnsyncedLines()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            PlainLyrics = "First line\nSecond line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("First line");
        candidate.Lines[0].Time.Total.Should().Be(0);
        candidate.Lines[1].Text.Should().Be("Second line");
        candidate.Lines[1].Time.Total.Should().Be(0);
    }

    [Fact]
    public void ToCandidate_UnrecognizedTimestamp_TreatsAsUnsynced()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = "Line without timestamp\n[00:01.00]Timed line",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("Line without timestamp");
        candidate.Lines[0].Time.Total.Should().Be(0);
        candidate.Lines[1].Text.Should().Be("Timed line");
        candidate.Lines[1].Time.Total.Should().Be(1.0);
    }

    [Fact]
    public void ToCandidate_ParsesTimestampCorrectly()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 300,
            Instrumental = false,
            SyncedLyrics = "[02:15.42]Line at 2:15.42",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines[0].Time.Minutes.Should().Be(2);
        candidate.Lines[0].Time.Seconds.Should().Be(15);
        candidate.Lines[0].Time.Hundredths.Should().Be(42);
        candidate.Lines[0].Time.Total.Should().Be(135.42);
    }

    [Fact]
    public void ToCandidate_StripsSurroundingWhitespace()
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = "  [00:01.00]  Text with spaces  ",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate!.Lines[0].Text.Should().Be("Text with spaces");
    }

    [Theory]
    [InlineData("")]
    public void ToCandidate_WithEmptyOrNullSyncedLyrics_UsesPlainsyncedIfAvailable(string synced)
    {
        LrclibSongResult result = new()
        {
            TrackName = "Song",
            ArtistName = "Artist",
            Duration = 100,
            Instrumental = false,
            SyncedLyrics = synced ?? "",
            PlainLyrics = "Plain lyrics available",
        };

        LyricCandidate? candidate = LrclibClient.ToCandidate(result);

        candidate.Should().NotBeNull();
        candidate!.HasSyncedLyrics.Should().BeFalse();
    }
}
