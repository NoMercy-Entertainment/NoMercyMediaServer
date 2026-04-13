namespace NoMercy.Encoder.V3.Profiles;

using NoMercy.Encoder.V3.Codecs;

public record EncodingProfile(
    Ulid Id,
    string Name,
    OutputFormat Format,
    VideoOutput[] VideoOutputs,
    AudioOutput[] AudioOutputs,
    SubtitleOutput[] SubtitleOutputs,
    ThumbnailOutput? Thumbnails = null,
    int SchemaVersion = 1
);
