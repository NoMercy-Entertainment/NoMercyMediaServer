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
using NoMercy.Encoder.Pipeline.Stages;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Pipeline.Planners;

/// <summary>
/// Keep-all-audio proof: the reported defect is a 2-English-audio source (768k +
/// 960k) publishing only one track. This isolates the planner — given two source
/// audio streams that both match the profile's language filter, AudioPlanBuilder
/// must emit two plans at DISTINCT playlist paths (never collapse to one). If this
/// passes, the drop is downstream (decompose/bundle/publish), not here.
/// </summary>
public class AudioPlanBuilderKeepAllTests
{
    private static AudioStreamInfo Audio(int index, string lang, long kbps) =>
        new(
            Index: index,
            Codec: "eac3",
            Channels: 6,
            SampleRate: 48000,
            BitRateKbps: kbps,
            Language: lang,
            IsDefault: index == 1,
            IsForced: false
        );

    private static MediaInfo MediaWith(params AudioStreamInfo[] audio) =>
        new(
            FilePath: "/movie.mkv",
            Format: "matroska",
            Duration: TimeSpan.FromMinutes(48),
            OverallBitRateKbps: 8000,
            FileSizeBytes: 1,
            VideoStreams: [],
            AudioStreams: audio,
            SubtitleStreams: [],
            Chapters: []
        );

    private static AudioOutput AllLanguages(StreamPolicy policy = StreamPolicy.Transcode) =>
        new(
            Policy: policy,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: AllowedLanguages.All,
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );

    private static EncodingProfile ProfileWith(params AudioOutput[] audio) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "keep-all",
            Container: Container.HlsFmp4,
            Video: null,
            Audio: audio,
            Subtitles: []
        );

    [Fact]
    public void TwoEnglishAudioTracks_ProduceTwoDistinctPlans()
    {
        MediaInfo media = MediaWith(Audio(1, "eng", 768), Audio(2, "eng", 960));
        EncodingProfile profile = ProfileWith(AllLanguages());

        AudioOutputPlan[] plans = AudioPlanBuilder.Build(profile, media);

        plans.Should().HaveCount(2, "both English audio tracks must be kept, not just the default");
        plans
            .Select(PlaylistGenerator.AudioVariantKey)
            .Distinct()
            .Should()
            .HaveCount(2, "the two kept tracks must resolve to distinct playlist paths on disk");
    }

    [Fact]
    public void MultiLanguageAudio_KeepsEveryLanguage()
    {
        MediaInfo media = MediaWith(
            Audio(1, "eng", 768),
            Audio(2, "jpn", 640),
            Audio(3, "fra", 448)
        );
        EncodingProfile profile = ProfileWith(AllLanguages());

        AudioOutputPlan[] plans = AudioPlanBuilder.Build(profile, media);

        plans.Should().HaveCount(3);
        plans.Select(p => p.Language).Should().BeEquivalentTo(["eng", "jpn", "fra"]);
    }
}
