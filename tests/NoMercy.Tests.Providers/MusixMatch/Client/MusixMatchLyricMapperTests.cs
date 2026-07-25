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

using NoMercy.Providers.MusixMatch.Client;
using NoMercy.Providers.MusixMatch.Models;
using NoMercy.Providers.NoMercy.Models;

namespace NoMercy.Tests.Providers.MusixMatch.Client;

[Trait("Category", "Unit")]
public class MusixMatchLyricMapperTests
{
    [Fact]
    public void ToCandidate_WithNullResponse_ReturnsNull()
    {
        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(null);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullMessage_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new() { Message = null };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullBody_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new() { Message = new() { Body = null } };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullMacroCalls_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new() { Body = new() { MacroCalls = null } },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullSubtitlesGet_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new() { Body = new() { MacroCalls = new() { TrackSubtitlesGet = null } } },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullSubtitlesMessage_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new() { TrackSubtitlesGet = new() { Message = null } },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullSubtitlesBody_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new() { Message = new() { Body = null } },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithEmptySubtitleList_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new() { Body = new() { SubtitleList = [] } },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullSubtitleBodyInList_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new() { SubtitleList = [new() { Subtitle = null }] },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithEmptySubtitleBody_ReturnsNull()
    {
        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = [] } },
                                    ],
                                },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithValidLyrics_MapsLines()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "First line",
                Time = new()
                {
                    Total = 1.5,
                    Minutes = 0,
                    Seconds = 1,
                    Hundredths = 50,
                },
            },
            new()
            {
                Text = "Second line",
                Time = new()
                {
                    Total = 3.2,
                    Minutes = 0,
                    Seconds = 3,
                    Hundredths = 20,
                },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().NotBeNull();
        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("First line");
        candidate.Lines[0].Time.Total.Should().Be(1.5);
        candidate.Lines[1].Text.Should().Be("Second line");
        candidate.Lines[1].Time.Total.Should().Be(3.2);
    }

    [Fact]
    public void ToCandidate_PreservesMappedLineTimestamps()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new()
                {
                    Total = 2.75,
                    Minutes = 0,
                    Seconds = 2,
                    Hundredths = 75,
                },
            },
        ];

        MusixMatchSubtitleGet response = CreateResponseWithSubtitles(lines);

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.Lines[0].Time.Minutes.Should().Be(0);
        candidate.Lines[0].Time.Seconds.Should().Be(2);
        candidate.Lines[0].Time.Hundredths.Should().Be(75);
    }

    [Fact]
    public void ToCandidate_WithSyncedTimestamps_MarksAsSynced()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 1.5 },
            },
        ];

        MusixMatchSubtitleGet response = CreateResponseWithSubtitles(lines);

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.HasSyncedLyrics.Should().BeTrue();
    }

    [Fact]
    public void ToCandidate_WithAllZeroTimestamps_MarksAsUnsync()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 0 },
            },
        ];

        MusixMatchSubtitleGet response = CreateResponseWithSubtitles(lines);

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.HasSyncedLyrics.Should().BeFalse();
    }

    [Fact]
    public void ToCandidate_WithMatcherTrackData_MapsTitleArtistDuration()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 1.5 },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                        MatcherTrackGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    MusixMatchMusixMatchTrack = new()
                                    {
                                        TrackName = "Bohemian Rhapsody",
                                        ArtistName = "Queen",
                                        TrackLength = 354,
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().NotBeNull();
        candidate!.Title.Should().Be("Bohemian Rhapsody");
        candidate.Artist.Should().Be("Queen");
        candidate.DurationSeconds.Should().Be(354);
    }

    [Fact]
    public void ToCandidate_WithZeroDuration_ReturnsDurationNull()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 1.5 },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                        MatcherTrackGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    MusixMatchMusixMatchTrack = new()
                                    {
                                        TrackName = "Song",
                                        ArtistName = "Artist",
                                        TrackLength = 0,
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithoutMatcherTrackGet_UsesEmptyDefaults()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 1.5 },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                        MatcherTrackGet = null,
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().NotBeNull();
        candidate!.Title.Should().Be(string.Empty);
        candidate.Artist.Should().Be(string.Empty);
        candidate.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public void ToCandidate_WithNullTrack_UsesEmptyDefaults()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = 1.5 },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                        MatcherTrackGet = new()
                        {
                            Message = new() { Body = new() { MusixMatchMusixMatchTrack = null! } },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate.Should().NotBeNull();
        candidate!.Title.Should().Be(string.Empty);
        candidate.Artist.Should().Be(string.Empty);
    }

    [Fact]
    public void ToCandidate_CopiesAllLineFields()
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Verse 1",
                Time = new()
                {
                    Total = 5.25,
                    Minutes = 0,
                    Seconds = 5,
                    Hundredths = 25,
                },
            },
            new()
            {
                Text = "Verse 2",
                Time = new()
                {
                    Total = 10.75,
                    Minutes = 0,
                    Seconds = 10,
                    Hundredths = 75,
                },
            },
        ];

        MusixMatchSubtitleGet response = CreateResponseWithSubtitles(lines);

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.Lines.Should().HaveCount(2);
        candidate.Lines[0].Text.Should().Be("Verse 1");
        candidate.Lines[0].Time.Total.Should().Be(5.25);
        candidate.Lines[0].Time.Minutes.Should().Be(0);
        candidate.Lines[0].Time.Seconds.Should().Be(5);
        candidate.Lines[0].Time.Hundredths.Should().Be(25);
        candidate.Lines[1].Text.Should().Be("Verse 2");
        candidate.Lines[1].Time.Total.Should().Be(10.75);
        candidate.Lines[1].Time.Minutes.Should().Be(0);
        candidate.Lines[1].Time.Seconds.Should().Be(10);
        candidate.Lines[1].Time.Hundredths.Should().Be(75);
    }

    [Fact]
    public void ToCandidate_WithMultipleSubtitlesInList_UsesFirst()
    {
        MusixMatchFormattedLyric[] linesA =
        [
            new()
            {
                Text = "First subtitle",
                Time = new() { Total = 1.0 },
            },
        ];
        MusixMatchFormattedLyric[] linesB =
        [
            new()
            {
                Text = "Second subtitle",
                Time = new() { Total = 1.0 },
            },
        ];

        MusixMatchSubtitleGet response = new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = linesA } },
                                        new() { Subtitle = new() { SubtitleBody = linesB } },
                                    ],
                                },
                            },
                        },
                    },
                },
            },
        };

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        candidate!.Lines[0].Text.Should().Be("First subtitle");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(0.5)]
    public void ToCandidate_HasSyncedLyricsDetectsAnyPositiveTimestamp(double timestamp)
    {
        MusixMatchFormattedLyric[] lines =
        [
            new()
            {
                Text = "Line",
                Time = new() { Total = timestamp },
            },
        ];

        MusixMatchSubtitleGet response = CreateResponseWithSubtitles(lines);

        LyricCandidate? candidate = MusixMatchLyricMapper.ToCandidate(response);

        bool expectedSynced = timestamp > 0;
        candidate!.HasSyncedLyrics.Should().Be(expectedSynced);
    }

    private static MusixMatchSubtitleGet CreateResponseWithSubtitles(
        MusixMatchFormattedLyric[] lines
    ) =>
        new()
        {
            Message = new()
            {
                Body = new()
                {
                    MacroCalls = new()
                    {
                        TrackSubtitlesGet = new()
                        {
                            Message = new()
                            {
                                Body = new()
                                {
                                    SubtitleList =
                                    [
                                        new() { Subtitle = new() { SubtitleBody = lines } },
                                    ],
                                },
                            },
                        },
                    },
                },
            },
        };
}
