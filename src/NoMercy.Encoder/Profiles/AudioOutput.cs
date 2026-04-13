namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Audio;
using NoMercy.Encoder.Codecs;

public record AudioOutput(
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    LoudnessMode Loudness = LoudnessMode.None
);
