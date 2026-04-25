namespace NoMercy.Tests.Encoder.Pipeline.Stages;

using NoMercy.Encoder.ContentAnalysis;

/// <summary>
/// Crop detector stub for tests that need a PlanStage but don't exercise
/// crop detection. Always returns <see cref="CropResult.ShouldCrop"/> =
/// <c>false</c> so no <c>crop=</c> filter is emitted.
/// </summary>
internal sealed class NoOpCropDetector : ICropDetector
{
    public Task<CropResult> DetectAsync(string inputPath, CancellationToken ct) =>
        Task.FromResult(new CropResult(0, 0, 0, 0, ShouldCrop: false));

    public Task<CropResult> DetectAsync(
        string inputPath,
        Guid? sourceVideoFileId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            new CropResult(0, 0, 0, 0, ShouldCrop: false, SourceVideoFileId: sourceVideoFileId)
        );
}
