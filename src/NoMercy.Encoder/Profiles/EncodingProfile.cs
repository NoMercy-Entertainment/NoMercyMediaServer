namespace NoMercy.Encoder.Profiles;

using NoMercy.Encoder.Codecs;

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
