namespace NoMercy.Encoder.V3.Commands;

public record GlobalOptions(
    bool Overwrite = true,
    bool HideBanner = true,
    bool ProgressPipe = true,
    int? Threads = null,
    long? ProbeSizeBytes = null,
    long? AnalyzeDurationUs = null
);
