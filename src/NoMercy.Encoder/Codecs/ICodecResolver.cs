using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Codecs;

public interface ICodecResolver
{
    ResolvedCodec Resolve(
        VideoCodecType codec,
        IHardwareCapabilities hardware,
        EncoderPreference preference = EncoderPreference.PreferHardware
    );

    /// <summary>
    /// Build a <see cref="ResolvedCodec"/> from a specific FFmpeg encoder name
    /// (e.g. when <see cref="IHardwarePreferenceResolver"/> has already picked
    /// hevc_nvenc from the SpeedIndex). Bypasses the IHardwareCapabilities.HasGpu
    /// gate so a stale / under-detected hardware probe can't override an
    /// encoder that the SpeedIndex has actually measured.
    /// </summary>
    ResolvedCodec ResolveByEncoderName(
        VideoCodecType codec,
        string ffmpegEncoderName,
        IHardwareCapabilities hardware
    );
}
