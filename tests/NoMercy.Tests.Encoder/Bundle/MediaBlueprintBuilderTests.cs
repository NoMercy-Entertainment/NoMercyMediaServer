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

using Newtonsoft.Json.Linq;
using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;

namespace NoMercy.Tests.Encoder.Bundle;

/// <summary>
/// Proves the foundation-slice invariant of the reconstruction-blueprint
/// spec: a full <see cref="MediaBlueprint"/> is generatable straight from a
/// <see cref="MediaInfo"/> analysis, with zero encode outputs. See
/// .claude/specs/reconstruction-blueprint/SPEC.md.
/// </summary>
public class MediaBlueprintBuilderTests
{
    private static readonly JObject SampleFfprobe = JObject.Parse(
        json: """
              {
                "format": { "format_name": "matroska,webm", "duration": "1440.050000" },
                "streams": [
                  { "index": 0, "codec_type": "video", "codec_name": "hevc" },
                  { "index": 1, "codec_type": "audio", "codec_name": "flac", "tags": { "language": "jpn" } },
                  { "index": 2, "codec_type": "subtitle", "codec_name": "ass", "tags": { "language": "eng" } }
                ],
                "chapters": []
              }
              """
    );

    private static MediaInfo BuildSourceMediaInfo() =>
        new(
            FilePath: "Download/complete/Frieren/[Judas] Frieren - S01E01.mkv",
            Format: "matroska,webm",
            Duration: TimeSpan.FromSeconds(value: 1440.05),
            OverallBitRateKbps: 35000,
            FileSizeBytes: 6_328_934,
            VideoStreams:
            [
                new(
                    Index: 0,
                    Codec: "hevc",
                    Width: 1920,
                    Height: 1080,
                    FrameRate: 23.976,
                    BitDepth: 10,
                    PixelFormat: "yuv420p10le",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 30000
                ),
            ],
            AudioStreams:
            [
                new(
                    Index: 1,
                    Codec: "flac",
                    Channels: 2,
                    SampleRate: 48000,
                    BitRateKbps: 0,
                    Language: "jpn",
                    IsDefault: true,
                    IsForced: false
                ),
            ],
            SubtitleStreams:
            [
                new(Index: 2, Codec: "ass", Language: "eng", IsDefault: false, IsForced: false),
            ],
            Chapters: [],
            Ffprobe: SampleFfprobe
        );

    private static BlueprintIdentity EpisodeIdentity() =>
        BlueprintIdentityFactory.From(
            media: new EpisodeMediaRef(
                Type: MediaType.Episode,
                Id: 4807632,
                Title: "OO Magic Episode 1",
                Year: 2023,
                ShowTitle: "Frieren: Beyond Journey's End",
                SeasonNumber: 1,
                EpisodeNumber: 1,
                ShowTmdbId: 209867
            )
        );

    // ── Full manifest, zero encode outputs ─────────────────────────────────

    [Fact]
    public void BuildFromSource_SourceFieldsMatchMediaInfo()
    {
        MediaInfo source = BuildSourceMediaInfo();
        MediaBlueprintBuilder builder = new();

        MediaBlueprint blueprint = builder.BuildFromSource(source: source, identity: EpisodeIdentity());

        blueprint.Source.Path.Should().Be(expected: source.FilePath);
        blueprint.Source.Filename.Should().Be(expected: "[Judas] Frieren - S01E01.mkv");
        blueprint.Source.Container.Should().Be(expected: "matroska,webm");
        blueprint.Source.SizeBytes.Should().Be(expected: 6_328_934);
        blueprint.Source.DurationSeconds.Should().Be(expected: 1440.05);
    }

    [Fact]
    public void BuildFromSource_Sha256IsNullThisSlice()
    {
        MediaInfo source = BuildSourceMediaInfo();
        MediaBlueprintBuilder builder = new();

        MediaBlueprint blueprint = builder.BuildFromSource(source: source, identity: EpisodeIdentity());

        // sha256 is deferred until a streaming hasher is wired into the
        // analyzer pipeline — see SPEC.md "Open items". Documenting the null
        // here so a future slice that flips it on has a failing test to fix.
        blueprint.Source.Sha256.Should().BeNull();
    }

    [Fact]
    public void BuildFromSource_FfprobeIsPreservedVerbatim()
    {
        MediaInfo source = BuildSourceMediaInfo();
        MediaBlueprintBuilder builder = new();

        MediaBlueprint blueprint = builder.BuildFromSource(source: source, identity: EpisodeIdentity());

        blueprint.Source.Ffprobe.Should().NotBeNull();
        JToken.DeepEquals(t1: blueprint.Source.Ffprobe, t2: SampleFfprobe).Should().BeTrue();
        blueprint.Source.Ffprobe![propertyName: "streams"]!
            .Should()
            .HaveCount(expected: 3, because: "video + audio + subtitle streams from the raw ffprobe output");
    }

    [Fact]
    public void BuildFromSource_EncodesIsEmpty()
    {
        MediaInfo source = BuildSourceMediaInfo();
        MediaBlueprintBuilder builder = new();

        MediaBlueprint blueprint = builder.BuildFromSource(source: source, identity: EpisodeIdentity());

        // The zero-encode-outputs proof: a complete, source-derived blueprint
        // with an empty encodes[] list.
        blueprint.Encodes.Should().BeEmpty();
        blueprint.Version.Should().Be(expected: 1);
    }

    // ── Identity mapping ────────────────────────────────────────────────────

    [Fact]
    public void BuildFromSource_EpisodeIdentity_MapsShowSeasonEpisode()
    {
        MediaInfo source = BuildSourceMediaInfo();
        MediaBlueprintBuilder builder = new();

        MediaBlueprint blueprint = builder.BuildFromSource(source: source, identity: EpisodeIdentity());

        blueprint.Identity.Type.Should().Be(expected: "episode");
        blueprint.Identity.TmdbId.Should().Be(expected: 4807632);
        blueprint.Identity.Show.Should().NotBeNull();
        blueprint.Identity.Show!.TmdbId.Should().Be(expected: 209867);
        blueprint.Identity.Show!.Title.Should().Be(expected: "Frieren: Beyond Journey's End");
        blueprint.Identity.Season.Should().Be(expected: 1);
        blueprint.Identity.Episode.Should().Be(expected: 1);
        blueprint.Identity.Title.Should().Be(expected: "OO Magic Episode 1");
        blueprint.Identity.Year.Should().Be(expected: 2023);
    }

    [Fact]
    public void BlueprintIdentityFactory_Movie_ProducesNullShowSeasonEpisode()
    {
        MovieMediaRef movie = new(
            Type: MediaType.Movie,
            Id: 603,
            Title: "The Matrix",
            Year: 1999,
            Description: "A hacker discovers reality is a simulation."
        );

        BlueprintIdentity identity = BlueprintIdentityFactory.From(media: movie);

        identity.Type.Should().Be(expected: "movie");
        identity.TmdbId.Should().Be(expected: 603);
        identity.Title.Should().Be(expected: "The Matrix");
        identity.Year.Should().Be(expected: 1999);
        identity.Show.Should().BeNull();
        identity.Season.Should().BeNull();
        identity.Episode.Should().BeNull();
    }

    [Fact]
    public void BlueprintIdentityFactory_UnsupportedMediaType_Throws()
    {
        MediaItemRef track = new(Type: MediaType.Track, Id: 1, Title: "Some Track", Year: null);

        Action act = () => BlueprintIdentityFactory.From(media: track);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
