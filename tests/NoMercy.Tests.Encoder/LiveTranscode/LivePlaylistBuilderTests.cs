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
            Index: index,
            StartTime: TimeSpan.FromSeconds(value: startSec),
            Duration: TimeSpan.FromSeconds(value: durSec),
            FilePath: $"/tmp/{index}.ts",
            SizeBytes: 100
        );

    [Fact]
    public void Build_EmptySegments_EmitsEventPlaylistWithNoEntries()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXTM3U");
        playlist.Should().Contain(expected: "#EXT-X-TARGETDURATION:6");
        playlist.Should().Contain(expected: "#EXT-X-MEDIA-SEQUENCE:0");
        playlist.Should().Contain(expected: "#EXT-X-PLAYLIST-TYPE:EVENT");
        playlist.Should().NotContain(unexpected: "#EXT-X-ENDLIST");
        playlist.Should().NotContain(unexpected: "#EXTINF");
    }

    [Fact]
    public void Build_WithSegments_EmitsExtinfAndUrlPerSegment()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(index: 0, startSec: 0, durSec: 6), MakeSegment(index: 1, startSec: 6, durSec: 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXTINF:6.000,");
        playlist.Should().Contain(expected: "/seg/0.ts");
        playlist.Should().Contain(expected: "/seg/1.ts");
    }

    [Fact]
    public void Build_Complete_EmitsVodTypeAndEndlist()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(index: 0, startSec: 0, durSec: 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: true,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.Should().Contain(expected: "#EXT-X-ENDLIST");
        playlist.Should().NotContain(unexpected: "#EXT-X-PLAYLIST-TYPE:EVENT");
    }

    [Fact]
    public void Build_MediaSequence_FollowsFirstSegmentIndex()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(index: 5, startSec: 30, durSec: 6), MakeSegment(index: 6, startSec: 36, durSec: 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXT-X-MEDIA-SEQUENCE:5");
    }

    [Fact]
    public void Build_TargetDuration_UsesLongestSegmentWhenGreaterThanTarget()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(index: 0, startSec: 0, durSec: 9.2)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXT-X-TARGETDURATION:10");
    }

    [Fact]
    public void Build_EmptyUrlTemplate_Throws()
    {
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: ""
        );

        Action act = () => _builder.Build(request: request);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_IndexSubstitution_ReplacesAllIndexPlaceholders()
    {
        LivePlaylistRequest request = new(
            SessionId: "abc",
            Segments: [MakeSegment(index: 2, startSec: 12, durSec: 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/live/{index}.ts"
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "/live/2.ts");
    }

    [Fact]
    public void Build_WithTotalDuration_EmitsWholeRuntimeVodListingEverySegment()
    {
        // 20s total, 6s segments → 4 segments (6+6+6+2), VOD + ENDLIST, listed
        // up front regardless of how few have actually been produced (only one
        // buffered here). This is what lets the client show a full-length bar.
        LivePlaylistRequest request = new(
            SessionId: "s",
            Segments: [MakeSegment(index: 0, startSec: 0, durSec: 6)],
            TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
            IsComplete: false,
            SegmentUrlTemplate: "/seg/{index}.ts",
            TotalDuration: TimeSpan.FromSeconds(seconds: 20)
        );

        string playlist = _builder.Build(request: request);

        playlist.Should().Contain(expected: "#EXT-X-PLAYLIST-TYPE:VOD");
        playlist.Should().Contain(expected: "#EXT-X-ENDLIST");
        playlist.Should().NotContain(unexpected: "#EXT-X-PLAYLIST-TYPE:EVENT");
        playlist.Should().Contain(expected: "/seg/0.ts");
        playlist.Should().Contain(expected: "/seg/1.ts");
        playlist.Should().Contain(expected: "/seg/2.ts");
        playlist.Should().Contain(expected: "/seg/3.ts");
        playlist.Should().NotContain(unexpected: "/seg/4.ts");
        // Last segment is the 2s remainder, not a full 6s.
        playlist.Should().Contain(expected: "#EXTINF:2.000,");
    }

    [Fact]
    public void Build_InvariantCulture_AlwaysUsesDotAsDecimalSeparator()
    {
        CultureInfo prev = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new(name: "nl-NL");

            LivePlaylistRequest request = new(
                SessionId: "s",
                Segments: [MakeSegment(index: 0, startSec: 0, durSec: 6.5)],
                TargetSegmentDuration: TimeSpan.FromSeconds(seconds: 6),
                IsComplete: false,
                SegmentUrlTemplate: "/seg/{index}.ts"
            );

            string playlist = _builder.Build(request: request);

            playlist.Should().Contain(expected: "#EXTINF:6.500,");
            playlist.Should().NotContain(unexpected: "6,500");
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
            VideoPlaylistUri: "playlist.m3u8",
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            AudioRenditions: []
        );

        string master = _builder.BuildMaster(request: request);

        master.Should().Contain(expected: "#EXTM3U");
        master.Should().Contain(expected: "#EXT-X-STREAM-INF:");
        master.Should().Contain(expected: "RESOLUTION=1920x1080");
        master.Should().Contain(expected: "VIDEO-RANGE=SDR");
        // The video variant is the last line so the client fetches the live media
        // playlist relative to the master's own URL.
        master.Should().Contain(expected: "playlist.m3u8");
    }

    [Fact]
    public void BuildMaster_EmitsOneAudioMediaEntryPerRendition_WithDisplayNames()
    {
        LiveMasterPlaylistRequest request = new(
            VideoPlaylistUri: "playlist.m3u8",
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            AudioRenditions:
            [
                new(Language: "eng", Uri: "/2/Show/S01E01/audio_eng_aac/audio_eng_aac.m3u8", IsDefault: true),
                new(Language: "jpn", Uri: "/2/Show/S01E01/audio_jpn_aac/audio_jpn_aac.m3u8", IsDefault: false),
            ]
        );

        string master = _builder.BuildMaster(request: request);

        master.Should().Contain(expected: "TYPE=AUDIO");
        master.Should().Contain(expected: "LANGUAGE=\"eng\"");
        master.Should().Contain(expected: "LANGUAGE=\"jpn\"");
        master.Should().Contain(expected: "NAME=\"English\"");
        master.Should().Contain(expected: "NAME=\"Japanese\"");
        master.Should().Contain(expected: "URI=\"/2/Show/S01E01/audio_eng_aac/audio_eng_aac.m3u8\"");
        master.Should().Contain(expected: "URI=\"/2/Show/S01E01/audio_jpn_aac/audio_jpn_aac.m3u8\"");
        // The variant references the audio group so the player pairs them.
        master.Should().Contain(expected: "AUDIO=\"audio\"");
    }

    [Fact]
    public void BuildMaster_MarksOnlyTheDefaultRenditionDefaultYes()
    {
        LiveMasterPlaylistRequest request = new(
            VideoPlaylistUri: "playlist.m3u8",
            Width: 1280,
            Height: 720,
            BitrateKbps: 3000,
            AudioRenditions:
            [
                new(Language: "jpn", Uri: "/a/jpn.m3u8", IsDefault: false),
                new(Language: "eng", Uri: "/a/eng.m3u8", IsDefault: true),
            ]
        );

        string master = _builder.BuildMaster(request: request);

        // Exactly one DEFAULT=YES, and it is the English track even though Japanese
        // is listed first — the viewer's language opens by default.
        System.Text.RegularExpressions.Regex.Matches(input: master, pattern: "DEFAULT=YES").Count.Should().Be(expected: 1);
        string engLine = master.Split(separator: '\n').First(predicate: line => line.Contains(value: "LANGUAGE=\"eng\""));
        engLine.Should().Contain(expected: "DEFAULT=YES");
    }

    [Fact]
    public void BuildMaster_NoRenditions_OmitsAudioGroupFromVariant()
    {
        LiveMasterPlaylistRequest request = new(
            VideoPlaylistUri: "playlist.m3u8",
            Width: 1920,
            Height: 1080,
            BitrateKbps: 5000,
            AudioRenditions: []
        );

        string master = _builder.BuildMaster(request: request);

        master.Should().NotContain(unexpected: "TYPE=AUDIO");
        master.Should().NotContain(unexpected: "AUDIO=\"audio\"");
    }
}
