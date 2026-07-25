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

using System.Globalization;
using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Tests.Encoder.LiveTranscode;

public class LivePlaylistBuilderTests
{
    private readonly LivePlaylistBuilder _builder = new();

    private static Segment MakeSegment(int index, double startSec, double durSec) =>
        new(
            index,
            TimeSpan.FromSeconds(startSec),
            TimeSpan.FromSeconds(durSec),
            $"/tmp/{index}.ts",
            100
        );

    [Fact]
    public void Build_EmptySegments_EmitsEventPlaylistWithNoEntries()
    {
        LivePlaylistRequest request = new(
            "s",
            [],
            TimeSpan.FromSeconds(6),
            false,
            "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXTM3U");
        playlist.Should().Contain("#EXT-X-TARGETDURATION:6");
        playlist.Should().Contain("#EXT-X-MEDIA-SEQUENCE:0");
        playlist.Should().Contain("#EXT-X-PLAYLIST-TYPE:EVENT");
        playlist.Should().NotContain("#EXT-X-ENDLIST");
        playlist.Should().NotContain("#EXTINF");
    }

    [Fact]
    public void Build_WithSegments_EmitsExtinfAndUrlPerSegment()
    {
        LivePlaylistRequest request = new(
            "s",
            [MakeSegment(0, 0, 6), MakeSegment(1, 6, 6)],
            TimeSpan.FromSeconds(6),
            false,
            "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXTINF:6.000,");
        playlist.Should().Contain("/seg/0.ts");
        playlist.Should().Contain("/seg/1.ts");
    }

    [Fact]
    public void Build_Complete_EmitsVodTypeAndEndlist()
    {
        LivePlaylistRequest request = new(
            "s",
            [MakeSegment(0, 0, 6)],
            TimeSpan.FromSeconds(6),
            true,
            "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.Should().Contain("#EXT-X-ENDLIST");
        playlist.Should().NotContain("#EXT-X-PLAYLIST-TYPE:EVENT");
    }

    [Fact]
    public void Build_MediaSequence_FollowsFirstSegmentIndex()
    {
        LivePlaylistRequest request = new(
            "s",
            [MakeSegment(5, 30, 6), MakeSegment(6, 36, 6)],
            TimeSpan.FromSeconds(6),
            false,
            "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-MEDIA-SEQUENCE:5");
    }

    [Fact]
    public void Build_TargetDuration_UsesLongestSegmentWhenGreaterThanTarget()
    {
        LivePlaylistRequest request = new(
            "s",
            [MakeSegment(0, 0, 9.2)],
            TimeSpan.FromSeconds(6),
            false,
            "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-TARGETDURATION:10");
    }

    [Fact]
    public void Build_EmptyUrlTemplate_Throws()
    {
        LivePlaylistRequest request = new(
            "s",
            [],
            TimeSpan.FromSeconds(6),
            false,
            ""
        );

        Action act = () => _builder.Build(request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_IndexSubstitution_ReplacesAllIndexPlaceholders()
    {
        LivePlaylistRequest request = new(
            "abc",
            [MakeSegment(2, 12, 6)],
            TimeSpan.FromSeconds(6),
            false,
            "/live/{index}.ts"
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("/live/2.ts");
    }

    [Fact]
    public void Build_WithTotalDuration_EmitsWholeRuntimeVodListingEverySegment()
    {
        // 20s total, 6s segments → 4 segments (6+6+6+2), VOD + ENDLIST, listed
        // up front regardless of how few have actually been produced (only one
        // buffered here). This is what lets the client show a full-length bar.
        LivePlaylistRequest request = new(
            "s",
            [MakeSegment(0, 0, 6)],
            TimeSpan.FromSeconds(6),
            false,
            "/seg/{index}.ts",
            TimeSpan.FromSeconds(20)
        );

        string playlist = _builder.Build(request);

        playlist.Should().Contain("#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.Should().Contain("#EXT-X-ENDLIST");
        playlist.Should().NotContain("#EXT-X-PLAYLIST-TYPE:EVENT");
        playlist.Should().Contain("/seg/0.ts");
        playlist.Should().Contain("/seg/1.ts");
        playlist.Should().Contain("/seg/2.ts");
        playlist.Should().Contain("/seg/3.ts");
        playlist.Should().NotContain("/seg/4.ts");
        // Last segment is the 2s remainder, not a full 6s.
        playlist.Should().Contain("#EXTINF:2.000,");
    }

    [Fact]
    public void Build_InvariantCulture_AlwaysUsesDotAsDecimalSeparator()
    {
        CultureInfo prev = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new("nl-NL");

            LivePlaylistRequest request = new(
                "s",
                [MakeSegment(0, 0, 6.5)],
                TimeSpan.FromSeconds(6),
                false,
                "/seg/{index}.ts"
            );

            string playlist = _builder.Build(request);

            playlist.Should().Contain("#EXTINF:6.500,");
            playlist.Should().NotContain("6,500");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prev;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BuildMaster — video variant + pre-encoded audio renditions
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildMaster_EmitsVideoVariantPointingAtRelativeMediaPlaylist()
    {
        LiveMasterPlaylistRequest request = new(
            "playlist.m3u8",
            1920,
            1080,
            5000,
            []
        );

        string master = _builder.BuildMaster(request);

        master.Should().Contain("#EXTM3U");
        master.Should().Contain("#EXT-X-STREAM-INF:");
        master.Should().Contain("RESOLUTION=1920x1080");
        master.Should().Contain("VIDEO-RANGE=SDR");
        // The video variant is the last line so the client fetches the live media
        // playlist relative to the master's own URL.
        master.Should().Contain("playlist.m3u8");
    }

    [Fact]
    public void BuildMaster_EmitsOneAudioMediaEntryPerRendition_WithDisplayNames()
    {
        LiveMasterPlaylistRequest request = new(
            "playlist.m3u8",
            1920,
            1080,
            5000,
            [
                new("eng", "/2/Show/S01E01/audio_eng_aac/audio_eng_aac.m3u8", true),
                new("jpn", "/2/Show/S01E01/audio_jpn_aac/audio_jpn_aac.m3u8", false),
            ]
        );

        string master = _builder.BuildMaster(request);

        master.Should().Contain("TYPE=AUDIO");
        master.Should().Contain("LANGUAGE=\"eng\"");
        master.Should().Contain("LANGUAGE=\"jpn\"");
        master.Should().Contain("NAME=\"English\"");
        master.Should().Contain("NAME=\"Japanese\"");
        master.Should().Contain("URI=\"/2/Show/S01E01/audio_eng_aac/audio_eng_aac.m3u8\"");
        master.Should().Contain("URI=\"/2/Show/S01E01/audio_jpn_aac/audio_jpn_aac.m3u8\"");
        // The variant references the audio group so the player pairs them.
        master.Should().Contain("AUDIO=\"audio\"");
    }

    [Fact]
    public void BuildMaster_MarksOnlyTheDefaultRenditionDefaultYes()
    {
        LiveMasterPlaylistRequest request = new(
            "playlist.m3u8",
            1280,
            720,
            3000,
            [
                new("jpn", "/a/jpn.m3u8", false),
                new("eng", "/a/eng.m3u8", true),
            ]
        );

        string master = _builder.BuildMaster(request);

        // Exactly one DEFAULT=YES, and it is the English track even though Japanese
        // is listed first — the viewer's language opens by default.
        System.Text.RegularExpressions.Regex.Matches(master, "DEFAULT=YES").Count.Should().Be(1);
        string engLine = master.Split('\n').First(line => line.Contains("LANGUAGE=\"eng\""));
        engLine.Should().Contain("DEFAULT=YES");
    }

    [Fact]
    public void BuildMaster_NoRenditions_OmitsAudioGroupFromVariant()
    {
        LiveMasterPlaylistRequest request = new(
            "playlist.m3u8",
            1920,
            1080,
            5000,
            []
        );

        string master = _builder.BuildMaster(request);

        master.Should().NotContain("TYPE=AUDIO");
        master.Should().NotContain("AUDIO=\"audio\"");
    }
}
