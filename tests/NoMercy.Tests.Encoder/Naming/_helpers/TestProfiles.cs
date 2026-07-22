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
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Naming;

internal static class TestProfiles
{
    public static EncodingProfile ArchiveRemuxMkv() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Archive Remux MKV",
            Container: Container.Mkv,
            Video: new(
                Policy: StreamPolicy.Copy,
                Codec: VideoCodecType.Copy,
                Width: 0,
                Height: null,
                RateControl: V2RateControlMode.Crf,
                Crf: 0,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: null,
                CodecProfile: CodecProfile.Auto,
                Level: null,
                Tune: null,
                BitDepth: 8,
                PixelFormat: null,
                KeyframeIntervalSeconds: 4,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/copy",
                PlaylistNameTemplate: "video/copy/playlist"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Copy,
                    Codec: AudioCodecType.Copy,
                    BitrateKbps: 0,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: AllowedLanguages.All,
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-copy",
                    PlaylistNameTemplate: "audio/{lang}-copy/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    Policy: SubtitlePolicy.Copy,
                    Codec: SubtitleCodecType.Copy,
                    AllowedLanguages: AllowedLanguages.All,
                    IncludeForced: true,
                    OcrLanguage: null,
                    PlaylistNameTemplate: "subs/{lang}"
                ),
            ]
        )
        {
            IsBuiltin = true,
        };

    public static EncodingProfile WebHls1080p() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Web 1080p",
            Container: Container.HlsFmp4,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
                Crf: 22,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: "yuv420p",
                KeyframeIntervalSeconds: 4,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio:
            [
                new(
                    Policy: StreamPolicy.Transcode,
                    Codec: AudioCodecType.Aac,
                    BitrateKbps: 192,
                    Channels: 2,
                    SampleRateHz: 48000,
                    AllowedLanguages: AllowedLanguages.All,
                    DefaultLanguage: null,
                    Loudness: null,
                    Downmix: null,
                    SegmentNameTemplate: "audio/{lang}-{codec}",
                    PlaylistNameTemplate: "audio/{lang}-{codec}/playlist"
                ),
            ],
            Subtitles:
            [
                new(
                    Policy: SubtitlePolicy.Extract,
                    Codec: SubtitleCodecType.WebVtt,
                    AllowedLanguages: AllowedLanguages.All,
                    IncludeForced: true,
                    OcrLanguage: null,
                    PlaylistNameTemplate: "subs/{lang}"
                ),
            ],
            Hls: new(),
            HlsDerivatives: new()
        )
        {
            IsBuiltin = true,
        };

    public static EncodingProfile WithContainer(Container container) =>
        new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Container: container,
            Video: null,
            Audio: [],
            Subtitles: []
        );

    public static EncodingProfile WithName(string name) =>
        new(
            Id: Ulid.NewUlid(),
            Name: name,
            Container: Container.HlsFmp4,
            Video: new(
                Policy: StreamPolicy.Transcode,
                Codec: VideoCodecType.H264,
                Width: 1920,
                Height: 1080,
                RateControl: V2RateControlMode.Crf,
                Crf: 22,
                BitrateKbps: 0,
                MaxBitrateKbps: null,
                BufferSizeKbps: null,
                Preset: "medium",
                CodecProfile: CodecProfile.High,
                Level: "4.0",
                Tune: null,
                BitDepth: 8,
                PixelFormat: "yuv420p",
                KeyframeIntervalSeconds: 4,
                ConvertHdrToSdr: false,
                SegmentNameTemplate: "video/{label}",
                PlaylistNameTemplate: "video/{label}/playlist"
            ),
            Audio: [],
            Subtitles: []
        );
}
