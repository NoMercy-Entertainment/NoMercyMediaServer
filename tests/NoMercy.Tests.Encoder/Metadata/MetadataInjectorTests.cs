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

using NoMercy.Encoder.Metadata;
using NoMercy.Encoder.Naming;

namespace NoMercy.Tests.Encoder.Metadata;

public class MetadataInjectorTests
{
    private readonly MetadataInjector _injector = new();

    // ------------------------------------------------------------------
    // Helper factories
    // ------------------------------------------------------------------

    private static MetadataInjectionContext MovieCtx(
        string title = "Fight Club",
        int? year = 1999,
        string? description = null,
        IReadOnlyList<TrackMetadata>? tracks = null,
        IReadOnlyList<string>? attachments = null
    ) =>
        new(
            Media: new MovieMediaRef(
                Type: MediaType.Movie,
                Id: 550,
                Title: title,
                Year: year,
                Description: description
            ),
            Tracks: tracks ?? [],
            AttachmentPaths: attachments ?? []
        );

    private static MetadataInjectionContext EpisodeCtx(
        string showTitle = "Breaking Bad",
        string episodeTitle = "Pilot",
        int seasonNumber = 1,
        int episodeNumber = 1,
        string? description = null,
        IReadOnlyList<TrackMetadata>? tracks = null
    ) =>
        new(
            Media: new EpisodeMediaRef(
                Type: MediaType.Episode,
                Id: 62085,
                Title: episodeTitle,
                Year: 2008,
                ShowTitle: showTitle,
                SeasonNumber: seasonNumber,
                EpisodeNumber: episodeNumber,
                Description: description
            ),
            Tracks: tracks ?? [],
            AttachmentPaths: []
        );

    // ------------------------------------------------------------------
    // F1-1: Movie → global -metadata flags
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_Movie_EmitsTitleAndYear()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx());

        int titleIdx = IndexOf(args: args, flag: "-metadata", value: "title=Fight Club");
        titleIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "expected -metadata title=Fight Club pair");

        int yearIdx = IndexOf(args: args, flag: "-metadata", value: "year=1999");
        yearIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "expected -metadata year=1999 pair");
    }

    [Fact]
    public void BuildArgs_Movie_NullYear_OmitsYearFlag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(year: null));

        bool hasYear = args.SkipWhile(predicate: a => a != "-metadata")
            .Skip(count: 1)
            .Take(count: 1)
            .Any(predicate: v => v.StartsWith(value: "year="));
        // Scan all -metadata pairs
        bool found = false;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[index: i] == "-metadata" && args[index: i + 1].StartsWith(value: "year="))
            {
                found = true;
                break;
            }
        }
        found.Should().BeFalse(because: "year=… must not appear when Year is null");
    }

    [Fact]
    public void BuildArgs_Movie_WithDescription_EmitsDescriptionFlag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(
            ctx: MovieCtx(description: "An insomniac office worker.")
        );

        int descIdx = IndexOf(args: args, flag: "-metadata", value: "description=An insomniac office worker.");
        descIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "expected -metadata description=… pair");
    }

    [Fact]
    public void BuildArgs_Movie_NullDescription_OmitsDescriptionFlag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(description: null));

        bool found = false;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[index: i] == "-metadata" && args[index: i + 1].StartsWith(value: "description="))
            {
                found = true;
                break;
            }
        }
        found.Should().BeFalse(because: "description flag must be absent when Description is null");
    }

    // ------------------------------------------------------------------
    // F1-2: Episode → extra show/season/episode flags
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_Episode_EmitsShowSeasonEpisodeFlags()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: EpisodeCtx());

        IndexOf(args: args, flag: "-metadata", value: "show=Breaking Bad")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected show= flag");
        IndexOf(args: args, flag: "-metadata", value: "season_number=1")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected season_number= flag");
        IndexOf(args: args, flag: "-metadata", value: "episode_id=1")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected episode_id= flag");
    }

    [Fact]
    public void BuildArgs_Episode_EmitsTitleFlag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: EpisodeCtx(episodeTitle: "Pilot"));

        IndexOf(args: args, flag: "-metadata", value: "title=Pilot")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "episode title flag expected");
    }

    // ------------------------------------------------------------------
    // F1-3: Per-stream language
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_AudioTrack_EmitsStreamLanguageFlag()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "audio",
            Language: "eng",
            Title: null,
            IsDefault: false,
            IsForced: false
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-metadata:s:a:0", value: "language=eng")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected -metadata:s:a:0 language=eng");
    }

    [Fact]
    public void BuildArgs_SubtitleTrack_UsesSubtitleStreamSpec()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "subtitle",
            Language: "fra",
            Title: null,
            IsDefault: false,
            IsForced: false
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-metadata:s:s:0", value: "language=fra")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "subtitle stream spec must use :s:");
    }

    [Fact]
    public void BuildArgs_VideoTrack_UsesVideoStreamSpec()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "video",
            Language: "eng",
            Title: null,
            IsDefault: false,
            IsForced: false
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-metadata:s:v:0", value: "language=eng")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "video stream spec must use :v:");
    }

    [Fact]
    public void BuildArgs_MultipleAudioTracks_EmitsIndexedSpecs()
    {
        TrackMetadata[] tracks =
        [
            new(OutputIndex: 0, Kind: "audio", Language: "eng", Title: null, IsDefault: true, IsForced: false),
            new(OutputIndex: 1, Kind: "audio", Language: "fra", Title: null, IsDefault: false, IsForced: false),
        ];
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: tracks));

        IndexOf(args: args, flag: "-metadata:s:a:0", value: "language=eng").Should().BeGreaterThanOrEqualTo(expected: 0);
        IndexOf(args: args, flag: "-metadata:s:a:1", value: "language=fra").Should().BeGreaterThanOrEqualTo(expected: 0);
    }

    // ------------------------------------------------------------------
    // F1-4: Disposition flags
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_DefaultAndForcedTrack_EmitsDispositionFlag()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "audio",
            Language: "eng",
            Title: null,
            IsDefault: true,
            IsForced: true
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-disposition:a:0", value: "default+forced")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected -disposition:a:0 default+forced");
    }

    [Fact]
    public void BuildArgs_DefaultOnlyTrack_EmitsDefaultDisposition()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "audio",
            Language: "eng",
            Title: null,
            IsDefault: true,
            IsForced: false
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-disposition:a:0", value: "default")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected -disposition:a:0 default");
    }

    [Fact]
    public void BuildArgs_ForcedOnlyTrack_EmitsForcedDisposition()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "subtitle",
            Language: "eng",
            Title: null,
            IsDefault: false,
            IsForced: true
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        IndexOf(args: args, flag: "-disposition:s:0", value: "forced")
            .Should()
            .BeGreaterThanOrEqualTo(expected: 0, because: "expected -disposition:s:0 forced");
    }

    [Fact]
    public void BuildArgs_NoDispositionSet_OmitsDispositionFlag()
    {
        TrackMetadata track = new(
            OutputIndex: 0,
            Kind: "audio",
            Language: "eng",
            Title: null,
            IsDefault: false,
            IsForced: false
        );
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(tracks: [track]));

        bool hasDisposition = args.Any(predicate: a => a.StartsWith(value: "-disposition:"));
        hasDisposition
            .Should()
            .BeFalse(because: "no -disposition flags when neither IsDefault nor IsForced");
    }

    // ------------------------------------------------------------------
    // F1-5: Cover-art attachment
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_WithAttachment_EmitsAttachAndMimetype()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(
            ctx: MovieCtx(attachments: ["/media/covers/cover.jpg"])
        );

        int attachIdx = Array.IndexOf(array: args.ToArray(), value: "-attach");
        attachIdx.Should().BeGreaterThanOrEqualTo(expected: 0, because: "expected -attach flag");
        args[index: attachIdx + 1].Should().Be(expected: "/media/covers/cover.jpg");

        // The mimetype metadata:s:t flag must appear after the -attach pair
        bool hasMime = false;
        for (int i = attachIdx + 2; i < args.Count - 1; i++)
        {
            if (args[index: i].StartsWith(value: "-metadata:s:t") && args[index: i + 1] == "mimetype=image/jpeg")
            {
                hasMime = true;
                break;
            }
        }
        hasMime.Should().BeTrue(because: "expected -metadata:s:t mimetype=image/jpeg after -attach");
    }

    [Fact]
    public void BuildArgs_WithPngAttachment_EmitsPngMimetype()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(
            ctx: MovieCtx(attachments: ["/media/covers/cover.png"])
        );

        bool hasMime = false;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[index: i].StartsWith(value: "-metadata:s:t") && args[index: i + 1] == "mimetype=image/png")
            {
                hasMime = true;
                break;
            }
        }
        hasMime.Should().BeTrue(because: "expected mimetype=image/png for .png attachment");
    }

    [Fact]
    public void BuildArgs_WithAttachment_EmitsFilenameTag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(
            ctx: MovieCtx(attachments: ["/media/covers/cover.jpg"])
        );

        bool hasFilename = false;
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[index: i].StartsWith(value: "-metadata:s:t") && args[index: i + 1].StartsWith(value: "filename="))
            {
                hasFilename = true;
                break;
            }
        }
        hasFilename.Should().BeTrue(because: "expected -metadata:s:t:M filename= tag");
    }

    [Fact]
    public void BuildArgs_NoAttachments_NoAttachFlag()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx(attachments: []));

        args.Should().NotContain(unexpected: "-attach");
    }

    // ------------------------------------------------------------------
    // F1-6: Empty context produces only global flags (no crash)
    // ------------------------------------------------------------------

    [Fact]
    public void BuildArgs_EmptyTracksAndAttachments_ProducesNoStreamFlags()
    {
        IReadOnlyList<string> args = _injector.BuildArgs(ctx: MovieCtx());

        bool hasStreamMeta = args.Any(predicate: a =>
            a.StartsWith(value: "-metadata:s:") || a.StartsWith(value: "-disposition:")
        );
        hasStreamMeta.Should().BeFalse(because: "no stream flags for empty track list");
    }

    // ------------------------------------------------------------------
    // Internal helper
    // ------------------------------------------------------------------

    /// <summary>Returns the index of <paramref name="flag"/> in <paramref name="args"/>
    /// where the next element equals <paramref name="value"/>, or -1.</summary>
    private static int IndexOf(IReadOnlyList<string> args, string flag, string value)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[index: i] == flag && args[index: i + 1] == value)
                return i;
        }
        return -1;
    }
}
