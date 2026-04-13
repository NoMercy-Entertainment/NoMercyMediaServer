namespace NoMercy.Encoder.Codecs;

using NoMercy.Encoder.Hardware;

public interface ICodecResolver
{
    ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    );
}
