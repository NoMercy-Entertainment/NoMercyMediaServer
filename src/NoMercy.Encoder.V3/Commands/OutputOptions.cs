namespace NoMercy.Encoder.V3.Commands;

public record OutputOptions(
    string FilePath,
    string? VideoCodec = null,
    string? AudioCodec = null,
    string? SubtitleCodec = null,
    int? VideoBitrateKbps = null,
    int? AudioBitrateKbps = null,
    int? Crf = null,
    string? Preset = null,
    string? Profile = null,
    string? Level = null,
    string? PixelFormat = null,
    int? KeyframeInterval = null,
    string? AudioChannels = null,
    int? AudioSampleRate = null,
    string[]? MapStreams = null,
    Dictionary<string, string>? ExtraFlags = null
);
