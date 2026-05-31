namespace NoMercy.Encoder.LiveTranscode;

public record Segment(
    int Index,
    TimeSpan StartTime,
    TimeSpan Duration,
    string FilePath,
    long SizeBytes
);
