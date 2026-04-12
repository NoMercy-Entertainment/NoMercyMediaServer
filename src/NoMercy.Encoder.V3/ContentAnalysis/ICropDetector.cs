namespace NoMercy.Encoder.V3.ContentAnalysis;

public interface ICropDetector
{
    Task<CropResult> DetectAsync(string inputPath, CancellationToken ct);
}

public record CropResult(int Width, int Height, int X, int Y, bool ShouldCrop);
