namespace NoMercy.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Codecs;

public record SubtitleOutput(SubtitleCodecType Codec, SubtitleMode Mode, string[] AllowedLanguages);
