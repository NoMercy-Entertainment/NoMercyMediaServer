namespace NoMercy.Encoder.Pipeline;

using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;

public interface IEncoder
{
    Task<EncodingResult> EncodeAsync(
        EncodingRequest request,
        IProgressObserver? progress = null,
        CancellationToken ct = default
    );

    Task<PreviewResult> PreviewAsync(
        EncodingRequest request,
        int previewDurationSeconds = 10,
        CancellationToken ct = default
    );
}

public record EncodingRequest(
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    EncodingOptions? Options = null,
    string? MediaTitle = null
)
{
    /// <summary>
    /// Title used for output file naming (master playlist, subtitles).
    /// When not set, derived from the output directory leaf name + ".NoMercy".
    /// </summary>
    public string ResolvedTitle =>
        MediaTitle
        ?? $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(OutputDirectory))}.NoMercy";
}

public record EncodingOptions(
    bool ResumeFromCheckpoint = false,
    int? MaxConcurrentEncodes = null,
    Priority Priority = Priority.Normal,
    EncodingPass Pass = EncodingPass.Single,
    string? StatsFilePath = null,
    int Pass1VariantIndex = 0
);

public enum Priority
{
    Normal,
    High,
}

/// <summary>
/// Which pass of a 2-pass encode this call represents. <see cref="Single"/>
/// is the default — the pipeline emits normal HLS output without any `-pass`
/// flag. <see cref="One"/> does video-only analysis writing to a stats file
/// (no HLS / audio / subtitle / sprite outputs). <see cref="Two"/> reads the
/// stats file and produces the final HLS encode with `-pass 2`.
/// </summary>
public enum EncodingPass
{
    Single,
    One,
    Two,
}

public record EncodingResult(
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    EncodingError? Error,
    EncodingMetrics Metrics
);

public record EncodingMetrics(
    long OutputSizeBytes,
    double AverageSpeed,
    double AverageFps,
    string EncoderUsed,
    string? GpuUsed
);
