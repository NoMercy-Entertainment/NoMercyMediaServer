namespace NoMercy.Encoder.Subtitles;

public record AcquiredSubtitle(
    string Language,
    string LocalPath,
    string Provider,
    bool IsExactMatch,
    double Rating,
    int Downloads,
    string Format
);
