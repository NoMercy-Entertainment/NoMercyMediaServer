namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Analysis;

public record LiveEncodeRequest(
    string InputPath,
    MediaInfo CachedInfo,
    ClientCapabilities Client,
    TimeSpan StartPosition,
    string? PreferredQuality
);
