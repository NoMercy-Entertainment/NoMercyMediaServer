namespace NoMercy.Encoder.SystemFeatures;

public interface IQualityVerifier
{
    Task<QualityCheckResult> VerifyAsync(
        string sourcePath,
        string encodedPath,
        CancellationToken ct
    );
}

public record QualityCheckResult(
    string SourcePath,
    string EncodedPath,
    double VmafScore,
    double Ssim,
    double Psnr,
    bool PassesThreshold
);
