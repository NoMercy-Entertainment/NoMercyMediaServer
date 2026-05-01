using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Codecs;

public interface ICodecResolver
{
    ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    );
}
