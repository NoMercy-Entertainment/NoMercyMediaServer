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

using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Codecs;

public class CodecResolver(CodecRegistry registry) : ICodecResolver
{
    public ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    )
    {
        ICodecDefinition definition = registry.GetVideoDefinition(codecType: codec);

        // Stream copy bypasses every preference branch. There is exactly one
        // synthetic encoder ("copy") in CopyVideoDefinition.Encoders, no
        // hardware path, no rate-control list (so MakeResolved would index
        // an empty array and crash). Hand back a Copy-shaped ResolvedCodec
        // directly with a sentinel rate-control mode the rest of the
        // pipeline never reads for copy outputs.
        if (codec == VideoCodecType.Copy)
        {
            EncoderInfo copyEncoder = definition.Encoders[0];
            return new(FfmpegEncoderName: copyEncoder.FfmpegName, EncoderInfo: copyEncoder, Device: null, DefaultRateControl: RateControlMode.Crf);
        }

        return preference switch
        {
            EncoderPreference.ForceSoftware => ResolveSoftware(definition: definition),
            EncoderPreference.ForceHardware => ResolveHardwareOrThrow(definition: definition, hardware: hardware),
            EncoderPreference.PreferQuality => ResolveSoftware(definition: definition),
            EncoderPreference.PreferSpeed => ResolvePreferHardware(definition: definition, hardware: hardware),
            _ => ResolvePreferHardware(definition: definition, hardware: hardware),
        };
    }

    private static ResolvedCodec ResolvePreferHardware(
        ICodecDefinition definition,
        IHardwareCapabilities hardware
    )
    {
        if (hardware.HasGpu)
        {
            foreach (GpuDevice gpu in hardware.Gpus)
            {
                EncoderInfo? hwEncoder = FindHardwareEncoder(definition: definition, vendor: gpu.Vendor);
                if (hwEncoder is not null && gpu.SupportedCodecs.Contains(value: definition.CodecType))
                    return MakeResolved(encoder: hwEncoder, device: gpu);
            }
        }
        return ResolveSoftware(definition: definition);
    }

    private static ResolvedCodec ResolveHardwareOrThrow(
        ICodecDefinition definition,
        IHardwareCapabilities hardware
    )
    {
        if (hardware.HasGpu)
        {
            foreach (GpuDevice gpu in hardware.Gpus)
            {
                EncoderInfo? hwEncoder = FindHardwareEncoder(definition: definition, vendor: gpu.Vendor);
                if (hwEncoder is not null && gpu.SupportedCodecs.Contains(value: definition.CodecType))
                    return MakeResolved(encoder: hwEncoder, device: gpu);
            }
        }
        throw new InvalidOperationException(
            message: $"No hardware encoder available for {definition.CodecType}. Use PreferHardware to allow software fallback."
        );
    }

    private static ResolvedCodec ResolveSoftware(ICodecDefinition definition)
    {
        EncoderInfo swEncoder = definition.Encoders.First(predicate: e => e.RequiredVendor is null);
        return MakeResolved(encoder: swEncoder, device: null);
    }

    private static EncoderInfo? FindHardwareEncoder(ICodecDefinition definition, GpuVendor vendor)
    {
        return definition.Encoders.FirstOrDefault(predicate: e => e.RequiredVendor == vendor);
    }

    public ResolvedCodec ResolveByEncoderName(
        VideoCodecType codec,
        string ffmpegEncoderName,
        IHardwareCapabilities hardware
    )
    {
        if (codec == VideoCodecType.Copy)
            return Resolve(codec: codec, hardware: hardware);

        ICodecDefinition definition = registry.GetVideoDefinition(codecType: codec);
        EncoderInfo? encoder = definition.Encoders.FirstOrDefault(predicate: e =>
            string.Equals(a: e.FfmpegName, b: ffmpegEncoderName, comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        if (encoder is null)
        {
            // Encoder name didn't match any definition entry — fall through to
            // the regular preference-based resolver so the pipeline gets a
            // best-effort match instead of crashing.
            return Resolve(codec: codec, hardware: hardware);
        }

        // Vendor-match a GPU device when available. When IHardwareCapabilities
        // hasn't detected one (stale probe / GPU enumeration gap) the SpeedIndex
        // has still validated the encoder works on this host — proceed without
        // the device handle. The pipeline tolerates null Device for hardware
        // encoders; only metrics labelling uses it.
        GpuDevice? device = encoder.RequiredVendor is { } vendor
            ? hardware.Gpus.FirstOrDefault(predicate: g => g.Vendor == vendor)
            : null;

        return MakeResolved(encoder: encoder, device: device);
    }

    private static ResolvedCodec MakeResolved(EncoderInfo encoder, GpuDevice? device)
    {
        RateControlMode defaultRc =
            encoder.SupportedRateControl.Contains(value: RateControlMode.Crf) ? RateControlMode.Crf
            : encoder.SupportedRateControl.Contains(value: RateControlMode.Cq) ? RateControlMode.Cq
            : encoder.SupportedRateControl.Contains(value: RateControlMode.Icq) ? RateControlMode.Icq
            : encoder.SupportedRateControl[0];

        return new(FfmpegEncoderName: encoder.FfmpegName, EncoderInfo: encoder, Device: device, DefaultRateControl: defaultRc);
    }
}
