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
        ICodecDefinition definition = registry.GetVideoDefinition(codec);

        // Stream copy bypasses every preference branch. There is exactly one
        // synthetic encoder ("copy") in CopyVideoDefinition.Encoders, no
        // hardware path, no rate-control list (so MakeResolved would index
        // an empty array and crash). Hand back a Copy-shaped ResolvedCodec
        // directly with a sentinel rate-control mode the rest of the
        // pipeline never reads for copy outputs.
        if (codec == VideoCodecType.Copy)
        {
            EncoderInfo copyEncoder = definition.Encoders[0];
            return new(copyEncoder.FfmpegName, copyEncoder, Device: null, RateControlMode.Crf);
        }

        return preference switch
        {
            EncoderPreference.ForceSoftware => ResolveSoftware(definition),
            EncoderPreference.ForceHardware => ResolveHardwareOrThrow(definition, hardware),
            EncoderPreference.PreferQuality => ResolveSoftware(definition),
            EncoderPreference.PreferSpeed => ResolvePreferHardware(definition, hardware),
            _ => ResolvePreferHardware(definition, hardware),
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
                EncoderInfo? hwEncoder = FindHardwareEncoder(definition, gpu.Vendor);
                if (hwEncoder is not null && gpu.SupportedCodecs.Contains(definition.CodecType))
                    return MakeResolved(hwEncoder, gpu);
            }
        }
        return ResolveSoftware(definition);
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
                EncoderInfo? hwEncoder = FindHardwareEncoder(definition, gpu.Vendor);
                if (hwEncoder is not null && gpu.SupportedCodecs.Contains(definition.CodecType))
                    return MakeResolved(hwEncoder, gpu);
            }
        }
        throw new InvalidOperationException(
            $"No hardware encoder available for {definition.CodecType}. Use PreferHardware to allow software fallback."
        );
    }

    private static ResolvedCodec ResolveSoftware(ICodecDefinition definition)
    {
        EncoderInfo swEncoder = definition.Encoders.First(e => e.RequiredVendor is null);
        return MakeResolved(swEncoder, null);
    }

    private static EncoderInfo? FindHardwareEncoder(ICodecDefinition definition, GpuVendor vendor)
    {
        return definition.Encoders.FirstOrDefault(e => e.RequiredVendor == vendor);
    }

    public ResolvedCodec ResolveByEncoderName(
        VideoCodecType codec,
        string ffmpegEncoderName,
        IHardwareCapabilities hardware
    )
    {
        if (codec == VideoCodecType.Copy)
            return Resolve(codec, hardware);

        ICodecDefinition definition = registry.GetVideoDefinition(codec);
        EncoderInfo? encoder = definition.Encoders.FirstOrDefault(e =>
            string.Equals(e.FfmpegName, ffmpegEncoderName, StringComparison.OrdinalIgnoreCase)
        );

        if (encoder is null)
        {
            // Encoder name didn't match any definition entry — fall through to
            // the regular preference-based resolver so the pipeline gets a
            // best-effort match instead of crashing.
            return Resolve(codec, hardware);
        }

        // Vendor-match a GPU device when available. When IHardwareCapabilities
        // hasn't detected one (stale probe / GPU enumeration gap) the SpeedIndex
        // has still validated the encoder works on this host — proceed without
        // the device handle. The pipeline tolerates null Device for hardware
        // encoders; only metrics labelling uses it.
        GpuDevice? device = encoder.RequiredVendor is { } vendor
            ? hardware.Gpus.FirstOrDefault(g => g.Vendor == vendor)
            : null;

        return MakeResolved(encoder, device);
    }

    private static ResolvedCodec MakeResolved(EncoderInfo encoder, GpuDevice? device)
    {
        RateControlMode defaultRc =
            encoder.SupportedRateControl.Contains(RateControlMode.Crf) ? RateControlMode.Crf
            : encoder.SupportedRateControl.Contains(RateControlMode.Cq) ? RateControlMode.Cq
            : encoder.SupportedRateControl.Contains(RateControlMode.Icq) ? RateControlMode.Icq
            : encoder.SupportedRateControl[0];

        return new(encoder.FfmpegName, encoder, device, defaultRc);
    }
}
