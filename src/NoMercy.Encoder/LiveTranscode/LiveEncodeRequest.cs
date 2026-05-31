using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public record LiveEncodeRequest(
    string InputPath,
    MediaInfo CachedInfo,
    ClientCapabilities Client,
    TimeSpan StartPosition,
    string? PreferredQuality
);
