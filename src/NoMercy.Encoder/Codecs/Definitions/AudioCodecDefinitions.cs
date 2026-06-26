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

namespace NoMercy.Encoder.Codecs.Definitions;

public static class AudioCodecDefinitions
{
    // AAC — "libfdk_aac" (Fraunhofer FDK; bundled in nomercy-ffmpeg with --enable-nonfree)
    // Lossy. Channels: mono/stereo/5.1/7.1. Sample rates: 44.1/48/96 kHz.
    // Bitrate: 32–512 kbps, default 192 kbps. Widely supported; preferred for streaming.
    private static readonly AudioEncoderInfo AacEncoder = new(
        FfmpegName: "libfdk_aac",
        CodecType: AudioCodecType.Aac,
        Channels: [1, 2, 6, 8],
        SampleRates: [44100, 48000, 96000],
        MinBitrateKbps: 32,
        MaxBitrateKbps: 512,
        DefaultBitrateKbps: 192,
        IsLossless: false
    );

    // FLAC — "flac"
    // Lossless. Channels: mono/stereo/5.1/7.1. Sample rates: 44.1/48/96/192 kHz.
    // No bitrate concept for lossless — all set to 0.
    private static readonly AudioEncoderInfo FlacEncoder = new(
        FfmpegName: "flac",
        CodecType: AudioCodecType.Flac,
        Channels: [1, 2, 6, 8],
        SampleRates: [44100, 48000, 96000, 192000],
        MinBitrateKbps: 0,
        MaxBitrateKbps: 0,
        DefaultBitrateKbps: 0,
        IsLossless: true
    );

    // Opus — "libopus"
    // Lossy. Channels: mono/stereo/5.1/7.1. Sample rate: 48 kHz only (always resamples internally).
    // Bitrate: 6–510 kbps, default 128 kbps. Best-in-class quality-per-bit at low bitrates.
    private static readonly AudioEncoderInfo OpusEncoder = new(
        FfmpegName: "libopus",
        CodecType: AudioCodecType.Opus,
        Channels: [1, 2, 6, 8],
        SampleRates: [48000],
        MinBitrateKbps: 6,
        MaxBitrateKbps: 510,
        DefaultBitrateKbps: 128,
        IsLossless: false
    );

    // AC3 — "ac3"
    // Lossy. Channels: mono/stereo/5.1 (no 7.1 in AC3). Sample rate: 48 kHz.
    // Bitrate: only the 19-step Dolby Digital ladder is accepted; ffmpeg
    // rounds down silently to the nearest rung for any off-ladder value.
    // Default 384 kbps — the Dolby sweet spot for 5.1 content.
    private static readonly AudioEncoderInfo Ac3Encoder = new(
        FfmpegName: "ac3",
        CodecType: AudioCodecType.Ac3,
        Channels: [1, 2, 6],
        SampleRates: [48000],
        MinBitrateKbps: 32,
        MaxBitrateKbps: 640,
        DefaultBitrateKbps: 384,
        IsLossless: false,
        ValidBitrateLadder:
        [
            32,
            40,
            48,
            56,
            64,
            80,
            96,
            112,
            128,
            160,
            192,
            224,
            256,
            320,
            384,
            448,
            512,
            576,
            640,
        ]
    );

    // E-AC3 — "eac3"
    // Lossy. Channels: mono/stereo/5.1/7.1. Sample rate: 48 kHz.
    // Bitrate: 32–6144 kbps, default 640 kbps. Dolby Digital Plus — supports Atmos metadata.
    private static readonly AudioEncoderInfo Eac3Encoder = new(
        FfmpegName: "eac3",
        CodecType: AudioCodecType.Eac3,
        Channels: [1, 2, 6, 8],
        SampleRates: [48000],
        MinBitrateKbps: 32,
        MaxBitrateKbps: 6144,
        DefaultBitrateKbps: 640,
        IsLossless: false
    );

    // MP3 — "libmp3lame"
    // Lossy. Channels: mono/stereo only (no surround). Sample rates: 44.1/48/96 kHz.
    // Bitrate: 32–320 kbps, default 192 kbps. Legacy compatibility codec.
    private static readonly AudioEncoderInfo Mp3Encoder = new(
        FfmpegName: "libmp3lame",
        CodecType: AudioCodecType.Mp3,
        Channels: [1, 2],
        SampleRates: [44100, 48000, 96000],
        MinBitrateKbps: 32,
        MaxBitrateKbps: 320,
        DefaultBitrateKbps: 192,
        IsLossless: false
    );

    // Vorbis — "libvorbis"
    // Lossy. Channels: mono/stereo/5.1/7.1. Sample rates: 44.1/48/96 kHz.
    // Bitrate: 45–500 kbps, default 192 kbps. Open standard; used in WebM/Ogg containers.
    private static readonly AudioEncoderInfo VorbisEncoder = new(
        FfmpegName: "libvorbis",
        CodecType: AudioCodecType.Vorbis,
        Channels: [1, 2, 6, 8],
        SampleRates: [44100, 48000, 96000],
        MinBitrateKbps: 45,
        MaxBitrateKbps: 500,
        DefaultBitrateKbps: 192,
        IsLossless: false
    );

    // TrueHD — "truehd"
    // Lossless. Channels: mono/stereo/5.1/7.1. Sample rates: 48/96 kHz.
    // No bitrate concept for lossless — all set to 0. Dolby TrueHD — Blu-ray lossless audio.
    private static readonly AudioEncoderInfo TrueHdEncoder = new(
        FfmpegName: "truehd",
        CodecType: AudioCodecType.TrueHd,
        Channels: [1, 2, 6, 8],
        SampleRates: [48000, 96000],
        MinBitrateKbps: 0,
        MaxBitrateKbps: 0,
        DefaultBitrateKbps: 0,
        IsLossless: true
    );

    // DTS — "dca"
    // Lossy. Channels: mono/stereo/5.1 (no 7.1 in base DTS). Sample rate: 48 kHz.
    // Bitrate: 32–1536 kbps, default 768 kbps. DTS core codec; ffmpeg encoder name is "dca".
    private static readonly AudioEncoderInfo DtsEncoder = new(
        FfmpegName: "dca",
        CodecType: AudioCodecType.Dts,
        Channels: [1, 2, 6],
        SampleRates: [48000],
        MinBitrateKbps: 32,
        MaxBitrateKbps: 1536,
        DefaultBitrateKbps: 768,
        IsLossless: false
    );

    // Copy — synthetic "encoder" for stream passthrough. ffmpeg name is the
    // literal "copy" pseudo-codec. Bitrate / channels / sample-rate are all
    // ignored at encode time (the copied bytes carry their own values), so
    // every range field is zero. IsLossless is true because no decode/encode
    // round-trip happens — the source bytes are remuxed unchanged.
    private static readonly AudioEncoderInfo CopyEncoder = new(
        FfmpegName: "copy",
        CodecType: AudioCodecType.Copy,
        Channels: [],
        SampleRates: [],
        MinBitrateKbps: 0,
        MaxBitrateKbps: 0,
        DefaultBitrateKbps: 0,
        IsLossless: true
    );

    private static readonly Dictionary<AudioCodecType, AudioEncoderInfo> EncoderMap = new()
    {
        [AudioCodecType.Aac] = AacEncoder,
        [AudioCodecType.Flac] = FlacEncoder,
        [AudioCodecType.Opus] = OpusEncoder,
        [AudioCodecType.Ac3] = Ac3Encoder,
        [AudioCodecType.Eac3] = Eac3Encoder,
        [AudioCodecType.Mp3] = Mp3Encoder,
        [AudioCodecType.Vorbis] = VorbisEncoder,
        [AudioCodecType.TrueHd] = TrueHdEncoder,
        [AudioCodecType.Dts] = DtsEncoder,
        [AudioCodecType.Copy] = CopyEncoder,
    };

    public static AudioEncoderInfo GetEncoder(AudioCodecType codecType) => EncoderMap[codecType];
}
