using NoMercy.Encoder.Codecs;

namespace NoMercy.Encoder.Profiles;

public interface IProfileFormProvider
{
    ProfileFormState GetFormState(PartialProfile selection);
    ValidationResult Validate(EncodingProfile profile);
}

public record PartialProfile(
    OutputFormat? Format = null,
    VideoCodecType? VideoCodec = null,
    AudioCodecType? AudioCodec = null
);

public record ProfileFormState(
    FormatOption[] Formats,
    VideoCodecOption[] VideoCodecs,
    AudioCodecOption[] AudioCodecs,
    SubtitleOption[] SubtitleOptions,
    VideoCodecParameters? VideoParams,
    AudioCodecParameters? AudioParams,
    FormatParameters? FormatParams,
    string[] ValidationMessages
);

public record FormatOption(OutputFormat Format, string DisplayName, bool IsAvailable);

public record VideoCodecOption(
    VideoCodecType Codec,
    string DisplayName,
    bool IsAvailable,
    string[] SupportedPresets
);

public record AudioCodecOption(
    AudioCodecType Codec,
    string DisplayName,
    bool IsAvailable,
    int[] SupportedChannels
);

public record SubtitleOption(string DisplayName, bool IsAvailable);

public record VideoCodecParameters(
    int? MinCrf,
    int? MaxCrf,
    string[] Presets,
    string[] Profiles,
    string[] Levels,
    bool SupportsTenBit,
    bool SupportsHdr
);

public record AudioCodecParameters(
    int MinBitrateKbps,
    int MaxBitrateKbps,
    int[] SupportedChannels,
    int[] SupportedSampleRates,
    bool IsLossless
);

public record FormatParameters(
    int? DefaultSegmentDurationSeconds,
    bool SupportsChapters,
    bool SupportsFonts,
    bool SupportsThumbnailSprites
);
