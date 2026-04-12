namespace NoMercy.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Analysis;

public record LiveEncodeRequest(
    string InputPath,
    MediaInfo CachedInfo,
    ClientCapabilities Client,
    TimeSpan StartPosition,
    string? PreferredQuality
);
