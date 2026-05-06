using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public record SubtitleOutput(
    SubtitlePolicy Policy,
    SubtitleCodecType Codec,
    string[] AllowedLanguages,
    bool IncludeForced,
    string? OcrLanguage,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
