namespace NoMercy.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Hardware;

public class CodecResolver(CodecRegistry registry) : ICodecResolver
{
    public ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    )
    {
        ICodecDefinition definition = registry.GetVideoDefinition(codec);

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

    private static ResolvedCodec MakeResolved(EncoderInfo encoder, GpuDevice? device)
    {
        RateControlMode defaultRc =
            encoder.SupportedRateControl.Contains(RateControlMode.Crf) ? RateControlMode.Crf
            : encoder.SupportedRateControl.Contains(RateControlMode.Cq) ? RateControlMode.Cq
            : encoder.SupportedRateControl.Contains(RateControlMode.Icq) ? RateControlMode.Icq
            : encoder.SupportedRateControl[0];

        return new ResolvedCodec(encoder.FfmpegName, encoder, device, defaultRc);
    }
}
