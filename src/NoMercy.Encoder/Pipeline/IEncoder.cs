using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Output;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Storage;

namespace NoMercy.Encoder.Pipeline;

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

    /// <summary>
    /// Run Analyze → Validate → Plan stages only (no ffmpeg process).
    /// Returns the <see cref="OutputPlan"/> so callers can call
    /// <see cref="IEncodingStrategy.Decompose"/> without running the full encode.
    /// Returns null when any stage fails.
    /// </summary>
    Task<OutputPlan?> PlanAsync(EncodingRequest request, CancellationToken ct = default);
}

public record EncodingRequest(
    string InputPath,
    string OutputDirectory,
    EncodingProfile Profile,
    EncodingOptions? Options = null,
    string? MediaTitle = null,
    /// <summary>
    /// Per-folder storage for the source file. When null the encoder falls
    /// back to the app-level singleton injected via DI. Set by encode jobs
    /// to the folder-scoped IStorage resolved by IStorageFactory so path
    /// guards and remote-driver routing are applied correctly.
    /// </summary>
    IStorage? SourceStorage = null,
    /// <summary>
    /// Per-folder storage for the encode output directory. Defaults to
    /// SourceStorage when null and SourceStorage is set; otherwise falls
    /// back to the DI singleton.
    /// When source and destination folders differ (e.g. source on SMB,
    /// output on local SSD) pass separate instances here.
    /// </summary>
    IStorage? DestinationStorage = null
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
    int Pass1VariantIndex = 0,
    /// <summary>
    /// When set, restricts the BuildStage to emit commands only for the
    /// outputs described by this task. Null means "emit everything" (normal
    /// full-plan execution). Set by the orchestrator when running a
    /// single decomposed child task.
    /// </summary>
    DecomposedTask? TaskFilter = null,
    /// <summary>
    /// When true the pipeline runs Analyze → Validate → Plan → Finalize,
    /// skipping Build + Execute. Used by the encode coordinator after all
    /// decomposed child tasks have written their slices to the shared
    /// tempDir — the coordinator then runs FinalizeStage once against the
    /// existing variant playlists / segments to produce the master m3u8,
    /// chapters.vtt, fonts.json, and bundle manifest. Concurrent per-task
    /// FinalizeStage runs would race those shared outputs and corrupt
    /// them, so per-task encodes leave FinalizeStage to this flag.
    /// </summary>
    bool FinalizeOnly = false,
    /// <summary>
    /// When non-null, BuildStage applies an input seek (<c>-ss</c>) to the
    /// primary input at the keyframe immediately before this position. Used
    /// by the resume path to restart the encode from near the last-known
    /// good position rather than from the beginning.
    /// </summary>
    long? ResumeFromMs = null
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

// EncodingResult and EncodingMetrics are defined in
// Orchestration/EncodingResult.cs (namespace NoMercy.Encoder.Pipeline)
// so both the pipeline and orchestration layers share the same type
// without a circular reference.
