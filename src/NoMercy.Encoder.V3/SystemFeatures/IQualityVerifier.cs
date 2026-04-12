namespace NoMercy.Encoder.V3.SystemFeatures;

public interface IQualityVerifier
{
    Task<QualityResult> VerifyAsync(string sourcePath, string encodedPath, CancellationToken ct);
}

public record QualityResult(double VmafScore, double Ssim, double Psnr, bool PassesThreshold);
