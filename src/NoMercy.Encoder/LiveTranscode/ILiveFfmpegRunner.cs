using NoMercy.Encoder.Analysis;

namespace NoMercy.Encoder.LiveTranscode;

public record LiveRunInput(
    string InputPath,
    string OutputDirectory,
    TimeSpan StartPosition,
    LiveQuality Quality,
    int SegmentDurationSeconds,
    ClientCapabilities? Client = null,
    MediaInfo? SourceInfo = null
);

/// <summary>
/// Spawns an FFmpeg HLS live transcode process for one session and feeds each
/// completed segment into the session's channel. Runs until the process exits
/// or the session is cancelled.
/// </summary>
public interface ILiveFfmpegRunner
{
    Task RunAsync(LiveRunInput input, LiveSession session, CancellationToken ct);
}
