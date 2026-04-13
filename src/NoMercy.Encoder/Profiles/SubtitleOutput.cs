namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Codecs;

public record SubtitleOutput(SubtitleCodecType Codec, SubtitleMode Mode, string[] AllowedLanguages);
