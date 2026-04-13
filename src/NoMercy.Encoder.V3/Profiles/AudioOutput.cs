namespace NoMercy.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Audio;
using NoMercy.Encoder.V3.Codecs;

public record AudioOutput(
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    LoudnessMode Loudness = LoudnessMode.None
);
