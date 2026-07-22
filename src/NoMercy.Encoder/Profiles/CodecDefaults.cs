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

namespace NoMercy.Encoder.Profiles;

using Codecs;

public static class CodecDefaults
{
    public record VideoDefaults(int Crf, string Preset, CodecProfile Profile, int BitDepth);

    public record AudioDefaults(int BitrateKbps, int Channels, int SampleRateHz);

    public static VideoDefaults For(VideoCodecType codec) =>
        codec switch
        {
            VideoCodecType.H264 => new(Crf: 22, Preset: "medium", Profile: CodecProfile.High, BitDepth: 8),
            VideoCodecType.H265 => new(Crf: 20, Preset: "slow", Profile: CodecProfile.Main10, BitDepth: 10),
            VideoCodecType.Av1 => new(Crf: 30, Preset: "6", Profile: CodecProfile.Main, BitDepth: 10),
            VideoCodecType.Vp9 => new(Crf: 32, Preset: "good", Profile: CodecProfile.Main, BitDepth: 8),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(codec),
                actualValue: codec,
                message: $"No defaults for {codec}"
            ),
        };

    public static AudioDefaults For(AudioCodecType codec) =>
        codec switch
        {
            AudioCodecType.Aac => new(BitrateKbps: 192, Channels: 2, SampleRateHz: 48000),
            AudioCodecType.Mp3 => new(BitrateKbps: 320, Channels: 2, SampleRateHz: 44100),
            AudioCodecType.Opus => new(BitrateKbps: 128, Channels: 2, SampleRateHz: 48000),
            AudioCodecType.Flac => new(BitrateKbps: 0, Channels: 2, SampleRateHz: 48000),
            AudioCodecType.Ac3 => new(BitrateKbps: 384, Channels: 6, SampleRateHz: 48000),
            AudioCodecType.Eac3 => new(BitrateKbps: 448, Channels: 6, SampleRateHz: 48000),
            AudioCodecType.TrueHd => new(BitrateKbps: 0, Channels: 6, SampleRateHz: 48000),
            AudioCodecType.Dts => new(BitrateKbps: 1536, Channels: 6, SampleRateHz: 48000),
            AudioCodecType.Vorbis => new(BitrateKbps: 192, Channels: 2, SampleRateHz: 48000),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(codec),
                actualValue: codec,
                message: $"No defaults for {codec}"
            ),
        };
}
