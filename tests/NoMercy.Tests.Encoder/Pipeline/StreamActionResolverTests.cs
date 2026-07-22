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
using NoMercy.Encoder.Pipeline;
using AudioOutput = NoMercy.Encoder.Profiles.AudioOutput;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using SubtitleOutput = NoMercy.Encoder.Profiles.SubtitleOutput;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;
using VideoOutput = NoMercy.Encoder.Profiles.VideoOutput;

namespace NoMercy.Tests.Encoder.Pipeline;

public class StreamActionResolverTests
{
    private readonly StreamActionResolver _resolver = new();

    // --- Audio ---

    [Fact]
    public void Audio_MatchingCodecAndSufficientBitrate_Copy()
    {
        AudioStreamInfo source = new(Index: 0, Codec: "aac", Channels: 2, SampleRate: 48000, BitRateKbps: 192, Language: "eng", IsDefault: true, IsForced: false);
        AudioOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 128,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver.ResolveAudio(source: source, profile: profile, format: OutputFormat.Mkv).Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public void Audio_DifferentCodec_Transcode()
    {
        AudioStreamInfo source = new(Index: 0, Codec: "ac3", Channels: 6, SampleRate: 48000, BitRateKbps: 640, Language: "eng", IsDefault: true, IsForced: false);
        AudioOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source: source, profile: profile, format: OutputFormat.Mkv)
            .Should()
            .Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Audio_LosslessSourceLossyTarget_AlwaysTranscode()
    {
        AudioStreamInfo source = new(Index: 0, Codec: "flac", Channels: 2, SampleRate: 48000, BitRateKbps: 900, Language: "eng", IsDefault: true, IsForced: false);
        AudioOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source: source, profile: profile, format: OutputFormat.Mkv)
            .Should()
            .Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Audio_InsufficientChannels_Transcode()
    {
        AudioStreamInfo source = new(Index: 0, Codec: "aac", Channels: 2, SampleRate: 48000, BitRateKbps: 192, Language: "eng", IsDefault: true, IsForced: false);
        AudioOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 6,
            SampleRateHz: 48000,
            AllowedLanguages: [],
            DefaultLanguage: null,
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );
        _resolver
            .ResolveAudio(source: source, profile: profile, format: OutputFormat.Mkv)
            .Should()
            .Be(expected: StreamAction.Transcode);
    }

    // --- Subtitle: text ---

    [Fact]
    public void Subtitle_TextSub_Mkv_Copy()
    {
        SubtitleStreamInfo source = new(Index: 0, Codec: "srt", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.Srt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver.ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Mkv).Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public void Subtitle_TextSub_Hls_Extract()
    {
        SubtitleStreamInfo source = new(Index: 0, Codec: "srt", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Hls)
            .Should()
            .Be(expected: StreamAction.Extract);
    }

    [Fact]
    public void Subtitle_TextSub_Mp4_Extract()
    {
        SubtitleStreamInfo source = new(Index: 0, Codec: "ass", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Mp4)
            .Should()
            .Be(expected: StreamAction.Extract);
    }

    // --- Subtitle: bitmap ---

    [Fact]
    public void Subtitle_BitmapSub_Mkv_Copy()
    {
        SubtitleStreamInfo source = new(Index: 0, Codec: "hdmv_pgs_subtitle", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver.ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Mkv).Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public void Subtitle_BitmapSub_Hls_Transcode()
    {
        // Bitmap subs for HLS must be burned in (mapped to Transcode)
        SubtitleStreamInfo source = new(Index: 0, Codec: "hdmv_pgs_subtitle", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Hls)
            .Should()
            .Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Subtitle_BurnInMode_AlwaysTranscode()
    {
        SubtitleStreamInfo source = new(Index: 0, Codec: "srt", Language: "eng", IsDefault: true, IsForced: false);
        SubtitleOutput profile = new(
            Policy: SubtitlePolicy.BurnIn,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: [],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
        _resolver
            .ResolveSubtitle(source: source, profile: profile, format: OutputFormat.Mkv)
            .Should()
            .Be(expected: StreamAction.Transcode);
    }

    // --- Video ---

    [Fact]
    public void Video_DifferentCodec_Transcode()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "hevc",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 8000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Video_SameCodecSameRes_Copy()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 8000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public void Video_SameCodecDifferentRes_Transcode()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "h264",
            Width: 3840,
            Height: 2160,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 20000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 8000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Video_HevcMain10SameResSufficientBitrate_Copy()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "hevc",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: "bt2020",
            ColorTransfer: "smpte2084",
            ColorSpace: "bt2020nc",
            IsDefault: true,
            BitRateKbps: 12000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 8000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 10,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Copy);
    }

    [Fact]
    public void Video_10BitSourceAgainst8BitProfile_Transcode()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "hevc",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 10,
            PixelFormat: "yuv420p10le",
            ColorPrimaries: "bt2020",
            ColorTransfer: "smpte2084",
            ColorSpace: "bt2020nc",
            IsDefault: true,
            BitRateKbps: 12000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H265,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 8000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Transcode);
    }

    [Fact]
    public void Video_8BitSourceAgainst10BitProfile_Transcode()
    {
        VideoStreamInfo source = new(
            Index: 0,
            Codec: "h264",
            Width: 1920,
            Height: 1080,
            FrameRate: 24.0,
            BitDepth: 8,
            PixelFormat: "yuv420p",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            IsDefault: true,
            BitRateKbps: 8000
        );
        VideoOutput profile = new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: RateControlMode.Crf,
            Crf: 0,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: null,
            CodecProfile: CodecProfile.Auto,
            Level: null,
            Tune: null,
            BitDepth: 10,
            PixelFormat: null,
            KeyframeIntervalSeconds: 0,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );
        _resolver.ResolveVideo(source: source, profile: profile).Should().Be(expected: StreamAction.Transcode);
    }
}
