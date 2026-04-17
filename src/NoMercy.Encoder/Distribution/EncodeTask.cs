namespace NoMercy.Encoder.Distribution;

using NoMercy.Encoder.Commands;

/// <summary>
/// How a strategy chose to split work across workers. <see cref="QualityVariant"/>
/// encodes a full variant (one resolution / bitrate tier) per task —
/// outputs stitch together as independent HLS variants with their own
/// playlists. <see cref="TimeChunk"/> encodes the same variant across
/// multiple time ranges — outputs must be stitched into a single playlist.
/// </summary>
public enum EncodeTaskType
{
    QualityVariant,
    TimeChunk,
}

public record EncodeTask(
    string TaskId,
    FfmpegCommand Command,
    string OutputPath,
    EncodeTaskType Type,
    TimeSpan? TimeRangeStart = null,
    TimeSpan? TimeRangeDuration = null,
    /// <summary>
    /// Absolute path to the source file the task reads from. Set so remote
    /// workers can detect when the path doesn't exist locally and fall
    /// back to fetching the source from the coordinator over HTTP. Null
    /// when the task doesn't carry a file input (synthetic / live sources)
    /// or when the coordinator + workers share storage and the input path
    /// is embedded directly in <see cref="Command"/>.
    /// </summary>
    string? InputPath = null
);

public record DispatchResult(
    string TaskId,
    bool Success,
    string OutputPath,
    TimeSpan Duration,
    string? Error = null,
    /// <summary>
    /// Which worker executed the task. Null for local dispatch; populated
    /// for remote workers so the orchestrator can attribute timing + per-
    /// worker failure stats without re-querying the registry.
    /// </summary>
    string? WorkerId = null
);
