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

using NoMercy.Encoder.Codecs.Definitions;

namespace NoMercy.Encoder.Codecs;

public class CodecRegistry
{
    private readonly Dictionary<VideoCodecType, ICodecDefinition> _videoDefinitions;
    private readonly Dictionary<string, EncoderInfo> _encodersByName;

    public CodecRegistry()
    {
        ICodecDefinition[] definitions =
        [
            new H264Definition(),
            new H265Definition(),
            new Av1Definition(),
            new Vp9Definition(),
            new CopyVideoDefinition(),
        ];

        _videoDefinitions = definitions.ToDictionary(keySelector: d => d.CodecType);

        _encodersByName = new();
        foreach (ICodecDefinition def in definitions)
        {
            foreach (EncoderInfo encoder in def.Encoders)
            {
                _encodersByName[key: encoder.FfmpegName] = encoder;
            }
        }
    }

    public ICodecDefinition GetVideoDefinition(VideoCodecType codecType) =>
        _videoDefinitions[key: codecType];

    public EncoderInfo? GetVideoEncoderByName(string ffmpegName) =>
        _encodersByName.GetValueOrDefault(key: ffmpegName);

    public AudioEncoderInfo GetAudioEncoder(AudioCodecType codecType) =>
        AudioCodecDefinitions.GetEncoder(codecType: codecType);

    public IEnumerable<(VideoCodecType CodecType, EncoderInfo Encoder)> EnumerateVideoEncoders()
    {
        foreach ((VideoCodecType codecType, ICodecDefinition def) in _videoDefinitions)
        {
            // Stream copy isn't a real encoder — benchmarking it would just
            // measure ffmpeg's mux throughput and pollute the speed index
            // with a dominant top-of-list entry that nothing should ever
            // pick from. Hide it from the enumeration consumed by
            // HardwareBenchmark / encoder-selection logic.
            if (codecType == VideoCodecType.Copy)
                continue;

            foreach (EncoderInfo encoder in def.Encoders)
                yield return (codecType, encoder);
        }
    }

    /// <summary>
    /// Returns true when the encoder handle is a hardware-accelerated encoder.
    /// Detection is based on well-known vendor suffixes rather than the registry
    /// because callers may supply handles that are not yet registered
    /// (e.g. from FfmpegCapabilities probes).
    /// </summary>
    public static bool IsHardware(string ffmpegEncoderName)
    {
        return ffmpegEncoderName.Contains(value: "_nvenc", comparisonType: StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains(value: "_qsv", comparisonType: StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains(value: "_amf", comparisonType: StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains(value: "_videotoolbox", comparisonType: StringComparison.OrdinalIgnoreCase)
            || ffmpegEncoderName.Contains(value: "_vaapi", comparisonType: StringComparison.OrdinalIgnoreCase);
    }
}
