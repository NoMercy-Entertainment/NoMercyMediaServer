using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles.V2;

public record AudioOutput(
    StreamPolicy Policy,
    AudioCodecType Codec,
    int BitrateKbps,
    int Channels,
    int SampleRateHz,
    string[] AllowedLanguages,
    string? DefaultLanguage,
    LoudnessConfig? Loudness,
    DownmixConfig? Downmix,
    string SegmentNameTemplate,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
