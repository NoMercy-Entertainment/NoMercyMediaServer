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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// V2ProfileFactory.FromV1 is the only authoritative DB-row → V2 profile
/// adapter. Drift here = stale presets silently changing behaviour after
/// migration. Tests pin the container-string parser, the encode-mode parser,
/// the codec classifier fallback, and the ladder construction across the
/// auto/manual/single-rung branches.
/// </summary>
public class V2ProfileFactoryTests
{
    private static V1VideoProfile MakeV1Video(
        string codec = "libx264",
        int width = 1920,
        int height = 1080,
        int bitrate = 5000,
        int crf = 0,
        string? colorSpace = null,
        string profile = "main"
    ) =>
        new(
            Codec: codec,
            Bitrate: bitrate,
            Width: width,
            Height: height,
            Preset: "medium",
            Profile: profile,
            Tune: "",
            Level: "",
            SegmentName: "",
            PlaylistName: "",
            ColorSpace: colorSpace ?? "",
            Crf: crf,
            KeyInt: 0,
            ConvertHdrToSdr: false,
            CustomArguments: []
        );

    private static V1AudioProfile MakeV1Audio(string codec = "aac", int channels = 2) =>
        new(
            Codec: codec,
            Channels: channels,
            SampleRate: 48000,
            SegmentName: "",
            PlaylistName: "",
            AllowedLanguages: ["eng"],
            CustomArguments: []
        );

    // ── Container string parser ─────────────────────────────────────────────

    [Theory]
    [InlineData("ts", Container.HlsTs)]
    [InlineData("mpegts", Container.HlsTs)]
    [InlineData("hls_ts", Container.HlsTs)]
    [InlineData("m3u8", Container.HlsFmp4)]
    [InlineData("hls", Container.HlsFmp4)]
    [InlineData("fmp4", Container.HlsFmp4)]
    [InlineData("hls_fmp4", Container.HlsFmp4)]
    [InlineData("mkv", Container.Mkv)]
    [InlineData("matroska", Container.Mkv)]
    [InlineData("mp4", Container.Mp4)]
    [InlineData("dash", Container.Dash)]
    [InlineData("mpd", Container.Dash)]
    [InlineData("mp3", Container.Mp3)]
    [InlineData("flac", Container.Flac)]
    [InlineData("ogg", Container.Ogg)]
    [InlineData("opus", Container.Ogg)]
    public void FromV1_ParsesContainerString(string raw, Container expected)
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: raw,
            videoProfiles: [MakeV1Video()],
            audioProfiles: [MakeV1Audio()],
            subtitleProfiles: []
        );

        profile.Container.Should().Be(expected);
    }

    [Fact]
    public void FromV1_UnknownContainerString_DefaultsToHlsFmp4()
    {
        // Unknown string MUST land somewhere safe — HlsFmp4 is the modern HLS
        // variant that supports HEVC, HDR, and is Apple+Google recommended.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "weird_unknown_thing",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Container.Should().Be(Container.HlsFmp4);
    }

    [Fact]
    public void FromV1_LegacyM3u8WithHevcSource_DefaultsToHlsFmp4()
    {
        // Pin the regression: legacy V1 rows often store 'm3u8' without saying
        // whether segments are TS or fMP4. Defaulting to HlsTs would fail the
        // codec/container validator for HEVC rows. HlsFmp4 default fixes that.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "m3u8",
            videoProfiles: [MakeV1Video(codec: "libx265")],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Container.Should().Be(Container.HlsFmp4);
        profile.Video!.Codec.Should().Be(VideoCodecType.H265);
    }

    // ── EncodeMode parser ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null, EncodeMode.SinglePass)]
    [InlineData("", EncodeMode.SinglePass)]
    [InlineData("   ", EncodeMode.SinglePass)]
    [InlineData("2pass", EncodeMode.TwoPass)]
    [InlineData("twopass", EncodeMode.TwoPass)]
    [InlineData("two_pass", EncodeMode.TwoPass)]
    [InlineData("two-pass", EncodeMode.TwoPass)]
    [InlineData("singlepass", EncodeMode.SinglePass)]
    [InlineData("garbage", EncodeMode.SinglePass)]
    public void FromV1_ParsesEncodeMode(string? raw, EncodeMode expected)
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [],
            subtitleProfiles: [],
            encodeMode: raw
        );

        profile.EncodeMode.Should().Be(expected);
    }

    // ── Ladder construction ─────────────────────────────────────────────────

    [Fact]
    public void FromV1_AutoLadderTrue_BuildsAutoLadder()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "hls",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [],
            subtitleProfiles: [],
            autoLadder: true
        );

        profile.Ladder.Should().NotBeNull();
        profile.Ladder!.Mode.Should().Be(LadderMode.Auto);
    }

    [Fact]
    public void FromV1_AutoLadderFalse_SingleVideoProfile_NoLadder()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "hls",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [],
            subtitleProfiles: [],
            autoLadder: false
        );

        profile.Ladder.Should().BeNull();
    }

    [Fact]
    public void FromV1_AutoLadderFalse_MultipleVideoProfiles_BuildsManualLadderFromExtras()
    {
        // First video is the reference; the remaining rows become ladder rungs.
        V1VideoProfile[] videos =
        [
            MakeV1Video(width: 1920, height: 1080, bitrate: 5000),
            MakeV1Video(width: 1280, height: 720, bitrate: 3000),
            MakeV1Video(width: 854, height: 480, bitrate: 1500),
        ];

        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "hls",
            videoProfiles: videos,
            audioProfiles: [],
            subtitleProfiles: [],
            autoLadder: false
        );

        profile.Ladder.Should().NotBeNull();
        profile.Ladder!.Mode.Should().Be(LadderMode.Manual);
        profile.Ladder.Rungs.Should().NotBeNull();
        profile.Ladder.Rungs!.Length.Should().Be(2);
        profile.Ladder.Rungs[0].Width.Should().Be(1280);
        profile.Ladder.Rungs[1].Width.Should().Be(854);
    }

    [Fact]
    public void FromV1_AutoLadderWinsOverMultipleVideoProfiles()
    {
        // autoLadder=true must trump the multi-video manual heuristic so the
        // ladder generator runs at PlanStage instead of using stale rungs.
        V1VideoProfile[] videos =
        [
            MakeV1Video(width: 1920, height: 1080),
            MakeV1Video(width: 1280, height: 720),
        ];

        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "hls",
            videoProfiles: videos,
            audioProfiles: [],
            subtitleProfiles: [],
            autoLadder: true
        );

        profile.Ladder!.Mode.Should().Be(LadderMode.Auto);
    }

    // ── Video mapping detail ────────────────────────────────────────────────

    [Fact]
    public void FromV1_CrfZero_UsesVbrRateControl()
    {
        // CRF=0 means the V1 row used bitrate-targeted encoding.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(crf: 0, bitrate: 4000)],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.RateControl.Should().Be(NoMercy.Encoder.Profiles.RateControlMode.Vbr);
    }

    [Fact]
    public void FromV1_CrfNonZero_UsesCrfRateControl()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(crf: 23, bitrate: 0)],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.RateControl.Should().Be(NoMercy.Encoder.Profiles.RateControlMode.Crf);
        profile.Video.Crf.Should().Be(23);
    }

    [Fact]
    public void FromV1_ColorSpaceContains10_Sets10BitDepth()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(colorSpace: "yuv420p10le")],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.BitDepth.Should().Be(10);
    }

    [Fact]
    public void FromV1_ColorSpaceContainsP010_Sets10BitDepth()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(colorSpace: "p010")],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.BitDepth.Should().Be(10);
    }

    [Fact]
    public void FromV1_NoColorSpaceMarker_Sets8BitDepth()
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(colorSpace: "yuv420p")],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.BitDepth.Should().Be(8);
    }

    [Fact]
    public void FromV1_NoVideoProfiles_LeavesVideoNull()
    {
        // Audio-only profile (FLAC / MP3 export) — no video.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "flac",
            videoProfiles: [],
            audioProfiles: [MakeV1Audio(codec: "flac")],
            subtitleProfiles: []
        );

        profile.Video.Should().BeNull();
    }

    // ── Audio mapping detail ────────────────────────────────────────────────

    [Theory]
    [InlineData("aac", AudioCodecType.Aac)]
    [InlineData("opus", AudioCodecType.Opus)]
    [InlineData("eac3", AudioCodecType.Eac3)]
    [InlineData("mp3", AudioCodecType.Mp3)]
    [InlineData("flac", AudioCodecType.Flac)]
    public void FromV1_ParsesAudioCodec(string codec, AudioCodecType expected)
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [MakeV1Audio(codec: codec)],
            subtitleProfiles: []
        );

        profile.Audio[0].Codec.Should().Be(expected);
    }

    [Fact]
    public void FromV1_UnknownAudioCodec_FallsBackToAac()
    {
        // Unknown audio codec must not throw — fall back to AAC, the only
        // codec guaranteed by every container we support.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [MakeV1Audio(codec: "garbage_codec_name")],
            subtitleProfiles: []
        );

        profile.Audio[0].Codec.Should().Be(AudioCodecType.Aac);
    }

    [Fact]
    public void FromV1_LosslessCodec_GetsZeroBitrate()
    {
        // FLAC bitrate is meaningless — the factory must zero it instead of
        // inheriting whatever junk value was in the V1 row.
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "flac",
            videoProfiles: [],
            audioProfiles: [MakeV1Audio(codec: "flac")],
            subtitleProfiles: []
        );

        profile.Audio[0].BitrateKbps.Should().Be(0);
    }

    [Fact]
    public void FromV1_LossyCodec_GetsDefaultBitrateFromEncoderInfo()
    {
        // Lossy codec → the encoder definition's default bitrate is applied.
        // Pinned: AAC default = 192 kbps (AudioCodecDefinitions.AacEncoder).
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [MakeV1Audio(codec: "aac")],
            subtitleProfiles: []
        );

        profile.Audio[0].BitrateKbps.Should().Be(192);
    }

    // ── Loudness parser ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ebu", LoudnessMode.EbuR128)]
    [InlineData("ebur128", LoudnessMode.EbuR128)]
    [InlineData("ebu_r128", LoudnessMode.EbuR128)]
    [InlineData("r128", LoudnessMode.EbuR128)]
    [InlineData("replaygain", LoudnessMode.ReplayGain)]
    [InlineData("replay_gain", LoudnessMode.ReplayGain)]
    [InlineData("rg", LoudnessMode.ReplayGain)]
    [InlineData("custom", LoudnessMode.Custom)]
    public void FromV1_ParsesLoudnessConfig(string raw, LoudnessMode expected)
    {
        V1AudioProfile audio = MakeV1Audio() with { Loudness = raw };

        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [audio],
            subtitleProfiles: []
        );

        profile.Audio[0].Loudness.Should().NotBeNull();
        profile.Audio[0].Loudness!.Mode.Should().Be(expected);
    }

    [Fact]
    public void FromV1_UnknownLoudness_LeavesNull()
    {
        V1AudioProfile audio = MakeV1Audio() with { Loudness = "weird" };

        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [audio],
            subtitleProfiles: []
        );

        profile.Audio[0].Loudness.Should().BeNull();
    }

    // ── Subtitle codec parser ───────────────────────────────────────────────

    [Theory]
    [InlineData("webvtt", SubtitleCodecType.WebVtt)]
    [InlineData("vtt", SubtitleCodecType.WebVtt)]
    [InlineData("ass", SubtitleCodecType.Ass)]
    [InlineData("ssa", SubtitleCodecType.Ass)]
    [InlineData("srt", SubtitleCodecType.Srt)]
    [InlineData("subrip", SubtitleCodecType.Srt)]
    [InlineData("copy", SubtitleCodecType.Copy)]
    [InlineData("passthrough", SubtitleCodecType.Copy)]
    public void FromV1_ParsesSubtitleCodec(string raw, SubtitleCodecType expected)
    {
        V1SubtitleProfile sub = new(
            Codec: raw,
            PlaylistName: "",
            AllowedLanguages: ["eng"],
            CustomArguments: []
        );

        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video()],
            audioProfiles: [],
            subtitleProfiles: [sub]
        );

        profile.Subtitles[0].Codec.Should().Be(expected);
    }

    // ── CodecProfile parser ─────────────────────────────────────────────────

    [Theory]
    [InlineData("baseline", CodecProfile.Baseline)]
    [InlineData("constrained_baseline", CodecProfile.Baseline)]
    [InlineData("main", CodecProfile.Main)]
    [InlineData("main10", CodecProfile.Main10)]
    [InlineData("Main 10", CodecProfile.Main10)]
    [InlineData("hi10p", CodecProfile.Main10)]
    [InlineData("high", CodecProfile.High)]
    [InlineData("high10", CodecProfile.High10)]
    [InlineData("garbage", CodecProfile.Auto)]
    public void FromV1_ParsesCodecProfile(string raw, CodecProfile expected)
    {
        EncodingProfile profile = V2ProfileFactory.FromV1(
            id: Ulid.NewUlid(),
            name: "p",
            container: "mp4",
            videoProfiles: [MakeV1Video(profile: raw)],
            audioProfiles: [],
            subtitleProfiles: []
        );

        profile.Video!.CodecProfile.Should().Be(expected);
    }
}
