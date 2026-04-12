namespace NoMercy.Encoder.V3.Analysis;

public record AudioStreamInfo(
    int Index,
    string Codec,
    int Channels,
    int SampleRate,
    long BitRateKbps,
    string? Language,
    bool IsDefault,
    bool IsForced
);
