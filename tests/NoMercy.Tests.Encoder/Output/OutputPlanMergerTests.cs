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

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Pipeline;

namespace NoMercy.Tests.Encoder.Output;

/// <summary>
/// The smart orchestrator's merge core: when a library folder carries several
/// encoding presets (e.g. "4K HDR HEVC" + "1080p SDR HEVC"), <see cref="OutputPlanMerger"/>
/// unions their independently-planned <see cref="OutputPlan"/>s into ONE plan
/// so the decomposition strategy produces a single coordinated encode instead
/// of one independent run per preset.
/// </summary>
public class OutputPlanMergerTests
{
    private static VideoOutputPlan MakeVideo(
        int width,
        int height,
        bool isHdrOutput,
        string encoderName = "libx265",
        string segmentTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
        string playlistTemplate = ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
    ) =>
        new(
            Width: width,
            Height: height,
            EncoderName: encoderName,
            Crf: 23,
            BitrateKbps: 0,
            Preset: "medium",
            Profile: "main",
            Level: "4.0",
            TenBit: false,
            PixelFormat: "yuv420p",
            MapLabel: "[v0]",
            ExtraFlags: [],
            SegmentNameTemplate: segmentTemplate,
            PlaylistNameTemplate: playlistTemplate,
            IsHdrOutput: isHdrOutput
        );

    private static AudioOutputPlan MakeAudio(
        string language,
        string encoderName = "aac",
        int sourceStreamIndex = 0
    ) =>
        new(
            EncoderName: encoderName,
            BitrateKbps: 128,
            Channels: 2,
            SampleRate: 48000,
            Action: StreamAction.Transcode,
            Language: language,
            MapLabel: "[a0]",
            SourceStreamIndex: sourceStreamIndex
        );

    private static SubtitleOutputPlan MakeSubtitle(string language) =>
        new(
            OutputCodec: SubtitleCodecType.WebVtt,
            Action: StreamAction.Extract,
            Language: language,
            SourceIndex: 0,
            MapLabel: null
        );

    // ------------------------------------------------------------------
    // (a) union of video renditions + one shared audio/subtitle/thumbnail/
    //     chapter set, taken from the primary plan.
    // ------------------------------------------------------------------

    [Fact]
    public void Merge_UnionsVideoRenditionsAcrossPlans()
    {
        OutputPlan fourKHdr = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 3840, height: 2160, isHdrOutput: true)],
            AudioOutputs: [MakeAudio(language: "eng")],
            SubtitleOutputs: [MakeSubtitle(language: "eng")],
            Thumbnails: new(Width: 160, Height: 68, IntervalSeconds: 10),
            Chapters: [new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 5), Title: "Chapter 1")]
        );
        OutputPlan sdr1080P = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [MakeAudio(language: "eng")],
            SubtitleOutputs: [MakeSubtitle(language: "fra")],
            Thumbnails: null,
            Chapters: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [fourKHdr, sdr1080P]);

        merged.VideoOutputs.Should().HaveCount(expected: 2);
        merged.VideoOutputs.Should().Contain(predicate: v => v.Width == 3840 && v.IsHdrOutput);
        merged.VideoOutputs.Should().Contain(predicate: v => v.Width == 1920 && !v.IsHdrOutput);
    }

    [Fact]
    public void Merge_DeduplicatesAudioByLanguageAndCodec()
    {
        // Both presets want "eng AAC 2.0" — that is the SAME rendition,
        // produced once, not twice.
        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 3840, height: 2160, isHdrOutput: true)],
            AudioOutputs: [MakeAudio(language: "eng")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [MakeAudio(language: "eng")],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2]);

        merged.AudioOutputs.Should().HaveCount(expected: 1);
        merged.AudioOutputs[0].Language.Should().Be(expected: "eng");
    }

    [Fact]
    public void Merge_KeepsTwoDistinctSourceTracksWithSameLanguageAndCodec()
    {
        // Regression for the missing-audio defect: a movie with two English
        // E-AC-3 tracks (e.g. a 768k and a 960k mix) produces two audio plans
        // that share language + codec but come from DIFFERENT source streams.
        // Merging must keep BOTH — keying the dedup on (language, codec) alone
        // silently dropped the second.
        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 3840, height: 2160, isHdrOutput: true)],
            AudioOutputs:
            [
                MakeAudio(language: "eng", encoderName: "eac3", sourceStreamIndex: 1),
                MakeAudio(language: "eng", encoderName: "eac3", sourceStreamIndex: 2),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs:
            [
                MakeAudio(language: "eng", encoderName: "eac3", sourceStreamIndex: 1),
                MakeAudio(language: "eng", encoderName: "eac3", sourceStreamIndex: 2),
            ],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2]);

        // Two distinct source tracks survive (the cross-preset duplicates of each
        // still dedup to one).
        merged.AudioOutputs.Should().HaveCount(expected: 2);
        merged
            .AudioOutputs.Select(selector: a => a.SourceStreamIndex)
            .Should()
            .BeEquivalentTo(expectation: [1, 2], because: "both distinct source audio tracks must survive the merge");
    }

    [Fact]
    public void Merge_KeepsAudioTracksWithDifferentCodecsForSameLanguage()
    {
        // One preset copies the source EAC3, the other transcodes to AAC —
        // different (language, codec) pairs describe DIFFERENT renditions.
        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 3840, height: 2160, isHdrOutput: true)],
            AudioOutputs: [MakeAudio(language: "eng", encoderName: "eac3")],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [MakeAudio(language: "eng", encoderName: "aac")],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2]);

        merged.AudioOutputs.Should().HaveCount(expected: 2);
        merged.AudioOutputs.Select(selector: a => a.CodecToken).Should().BeEquivalentTo(expectation: ["eac3", "aac"]);
    }

    [Fact]
    public void Merge_SharedFields_ComeFromThePrimaryPlanOnly()
    {
        SubtitleOutputPlan primarySubtitle = MakeSubtitle(language: "eng");
        ThumbnailOutputPlan primaryThumbnails = new(Width: 160, Height: 68, IntervalSeconds: 10);
        ChapterInfo[] primaryChapters = [new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 1), Title: "Ch1")];

        OutputPlan primary = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 3840, height: 2160, isHdrOutput: true)],
            AudioOutputs: [MakeAudio(language: "eng")],
            SubtitleOutputs: [primarySubtitle],
            Thumbnails: primaryThumbnails,
            Chapters: primaryChapters
        );
        OutputPlan secondary = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [MakeAudio(language: "eng")],
            // Deliberately different — must NOT surface in the merge, since
            // subtitles/thumbnails/chapters are source-derived and identical
            // across presets; duplicating them would double the OCR/extract
            // work this merge exists to avoid.
            SubtitleOutputs: [MakeSubtitle(language: "jpn"), MakeSubtitle(language: "kor")],
            Thumbnails: new(Width: 320, Height: 180, IntervalSeconds: 5),
            Chapters: [new(Start: TimeSpan.Zero, End: TimeSpan.FromMinutes(minutes: 2), Title: "Other")]
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [primary, secondary]);

        merged.SubtitleOutputs.Should().BeEquivalentTo(expectation: [primarySubtitle]);
        merged.Thumbnails.Should().BeEquivalentTo(expectation: primaryThumbnails);
        merged.Chapters.Should().BeEquivalentTo(expectation: primaryChapters);
    }

    [Fact]
    public void Merge_DifferentFormats_Throws()
    {
        OutputPlan hlsPlan = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan dashPlan = new(
            Format: OutputFormat.Dash,
            VideoOutputs: [MakeVideo(width: 1920, height: 1080, isHdrOutput: false)],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        Action act = () => OutputPlanMerger.Merge(plans: [hlsPlan, dashPlan]);

        act.Should().Throw<InvalidOperationException>();
    }

    // ------------------------------------------------------------------
    // (b) collision guard — same resolved on-disk folder gets disambiguated;
    //     distinct renditions are left untouched.
    // ------------------------------------------------------------------

    [Fact]
    public void Merge_SameResolutionAndRangeDifferentCodec_Disambiguates()
    {
        // Two presets both emit a 1080p SDR rendition, but with different
        // codecs — without disambiguation both would resolve to the same
        // "video_1920x1080_SDR/" folder and clobber each other's segments.
        VideoOutputPlan hevc1080P = MakeVideo(
            width: 1920,
            height: 1080,
            isHdrOutput: false,
            encoderName: "libx265"
        );
        VideoOutputPlan av1_1080P = MakeVideo(
            width: 1920,
            height: 1080,
            isHdrOutput: false,
            encoderName: "libsvtav1"
        );

        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [hevc1080P],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [av1_1080P],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2]);

        merged.VideoOutputs.Should().HaveCount(expected: 2);
        merged
            .VideoOutputs.Select(selector: v => v.PlaylistNameTemplate)
            .Should()
            .OnlyHaveUniqueItems(because: "colliding renditions must resolve to distinct on-disk folders");
        merged
            .VideoOutputs.Select(selector: v => v.SegmentNameTemplate)
            .Should()
            .OnlyHaveUniqueItems(because: "colliding renditions must resolve to distinct segment folders");
        // Both halves of the folder/file template get the same suffix, or the
        // segment files would still collide inside a disambiguated folder.
        merged
            .VideoOutputs.Should()
            .AllSatisfy(expected: v =>
            {
                string[] parts = v.PlaylistNameTemplate.Split(separator: '/');
                parts.Should().HaveCount(expected: 2);
                parts[0].Should().Be(expected: parts[1]);
            });
    }

    [Fact]
    public void Merge_DifferentResolutionOrRange_LeavesTemplatesUntouched()
    {
        // The Marvels case from the slice spec: 4K HDR + 1080p SDR never
        // collide (different width AND different HDR/SDR range) — the guard
        // must leave both templates exactly as PlanStage produced them.
        VideoOutputPlan fourKHdr = MakeVideo(width: 3840, height: 2160, isHdrOutput: true);
        VideoOutputPlan sdr1080P = MakeVideo(width: 1920, height: 1080, isHdrOutput: false);
        string originalFourKTemplate = fourKHdr.PlaylistNameTemplate;
        string originalSdrTemplate = sdr1080P.PlaylistNameTemplate;

        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [fourKHdr],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [sdr1080P],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2]);

        merged.VideoOutputs.Should().HaveCount(expected: 2);
        merged
            .VideoOutputs.First(predicate: v => v.Width == 3840)
            .PlaylistNameTemplate.Should()
            .Be(expected: originalFourKTemplate);
        merged
            .VideoOutputs.First(predicate: v => v.Width == 1920)
            .PlaylistNameTemplate.Should()
            .Be(expected: originalSdrTemplate);
    }

    [Fact]
    public void Merge_ThreeWayCollision_AllThreeGetDistinctFolders()
    {
        VideoOutputPlan h264 = MakeVideo(width: 1920, height: 1080, isHdrOutput: false, encoderName: "libx264");
        VideoOutputPlan hevc = MakeVideo(width: 1920, height: 1080, isHdrOutput: false, encoderName: "libx265");
        VideoOutputPlan av1 = MakeVideo(width: 1920, height: 1080, isHdrOutput: false, encoderName: "libsvtav1");

        OutputPlan preset1 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [h264],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset2 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [hevc],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );
        OutputPlan preset3 = new(
            Format: OutputFormat.Hls,
            VideoOutputs: [av1],
            AudioOutputs: [],
            SubtitleOutputs: [],
            Thumbnails: null
        );

        OutputPlan merged = OutputPlanMerger.Merge(plans: [preset1, preset2, preset3]);

        merged.VideoOutputs.Should().HaveCount(expected: 3);
        merged.VideoOutputs.Select(selector: v => v.PlaylistNameTemplate).Should().OnlyHaveUniqueItems();
    }
}
