using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles.V2;

public record SubtitleOutput(
    SubtitlePolicy Policy,
    SubtitleCodecType Codec,
    string[] AllowedLanguages,
    bool IncludeForced,
    string? OcrLanguage,
    string PlaylistNameTemplate,
    Dictionary<string, string>? CustomArguments = null
);
