namespace NoMercy.Encoder.ContentAnalysis;

public interface ICropDetector
{
    Task<CropResult> DetectAsync(string inputPath, CancellationToken ct);

    Task<CropResult> DetectAsync(string inputPath, Guid? sourceVideoFileId, CancellationToken ct);
}

/// <summary>
/// Result of a cropdetect pass.
/// <para>
/// <see cref="SourceVideoFileId"/> is supplied by callers that have a video
/// file id handy (e.g. the <c>POST /content-analysis/crop/{video_file_id}</c>
/// endpoint) so the response shape matches the spec without forcing the
/// detector itself to know about persistence.
/// </para>
/// <para>
/// <see cref="SampleFramesAnalyzed"/> is the number of cropdetect observations
/// that contributed to the picked rectangle — equals the count for the
/// most-frequent <c>crop=W:H:X:Y</c> stderr line.
/// </para>
/// <para>
/// <see cref="Confidence"/> is the fraction of cropdetect observations that
/// agreed with the picked rectangle (0..1). 1.0 means every observation saw
/// the same crop; lower values mean the source had inconsistent black-bar
/// behaviour. Below the <c>MinObservations</c> threshold the detector
/// already returns <see cref="ShouldCrop"/>=false, so this field is only
/// useful for callers that want to surface "low-confidence" crops in the UI.
/// </para>
/// </summary>
public record CropResult(
    int Width,
    int Height,
    int X,
    int Y,
    bool ShouldCrop,
    Guid? SourceVideoFileId = null,
    int SampleFramesAnalyzed = 0,
    double Confidence = 0
);
