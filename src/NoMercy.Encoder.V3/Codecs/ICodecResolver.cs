namespace NoMercy.Encoder.V3.Codecs;

using NoMercy.Encoder.V3.Hardware;

public interface ICodecResolver
{
    ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    );
}
