namespace NoMercy.Encoder.Analysis;

public record AudioStreamInfo(
    int Index,
    string Codec,
    int Channels,
    int SampleRate,
    long BitRateKbps,
    string? Language,
    bool IsDefault,
    bool IsForced,
    double StartTimeSeconds = 0,
    double DelayVsVideoSeconds = 0
);
