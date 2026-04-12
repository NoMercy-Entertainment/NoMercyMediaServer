namespace NoMercy.Tests.Encoder.V3.AdvancedFeatures;

using NoMercy.Encoder.V3.SystemFeatures;

public class SystemFeaturesTests
{
    [Fact]
    public void PatchResult_Success_ConstructsCorrectly()
    {
        PatchResult result = new(
            Success: true,
            Message: "Patch applied successfully.",
            RequiresRestart: true
        );

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Patch applied successfully.");
        result.RequiresRestart.Should().BeTrue();
    }

    [Fact]
    public void PatchResult_Failure_ConstructsCorrectly()
    {
        PatchResult result = new(
            Success: false,
            Message: "Patch failed: insufficient permissions.",
            RequiresRestart: false
        );

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
        result.RequiresRestart.Should().BeFalse();
    }

    [Fact]
    public void PatchResult_SuccessWithoutRestart_IsValid()
    {
        PatchResult result = new(
            Success: true,
            Message: "Already patched.",
            RequiresRestart: false
        );

        result.Success.Should().BeTrue();
        result.RequiresRestart.Should().BeFalse();
    }

    [Fact]
    public void QualityResult_PassesThreshold_WhenVmafIsHigh()
    {
        QualityResult result = new(VmafScore: 95.0, Ssim: 0.998, Psnr: 48.5, PassesThreshold: true);

        result.VmafScore.Should().BeApproximately(95.0, 0.01);
        result.Ssim.Should().BeApproximately(0.998, 0.0001);
        result.Psnr.Should().BeApproximately(48.5, 0.01);
        result.PassesThreshold.Should().BeTrue();
    }

    [Fact]
    public void QualityResult_FailsThreshold_WhenVmafIsLow()
    {
        QualityResult result = new(VmafScore: 55.0, Ssim: 0.92, Psnr: 28.0, PassesThreshold: false);

        result.VmafScore.Should().BeLessThan(70.0);
        result.PassesThreshold.Should().BeFalse();
    }

    [Fact]
    public void QualityResult_ZeroScores_IsValid()
    {
        QualityResult result = new(VmafScore: 0.0, Ssim: 0.0, Psnr: 0.0, PassesThreshold: false);

        result.VmafScore.Should().Be(0.0);
        result.Ssim.Should().Be(0.0);
        result.Psnr.Should().Be(0.0);
        result.PassesThreshold.Should().BeFalse();
    }

    [Fact]
    public void PipelineHook_HasAllExpectedValues()
    {
        PipelineHook[] values = Enum.GetValues<PipelineHook>();

        values.Should().Contain(PipelineHook.BeforeAnalyze);
        values.Should().Contain(PipelineHook.AfterAnalyze);
        values.Should().Contain(PipelineHook.BeforeBuild);
        values.Should().Contain(PipelineHook.AfterBuild);
        values.Should().Contain(PipelineHook.BeforeExecute);
        values.Should().Contain(PipelineHook.AfterExecute);
        values.Should().Contain(PipelineHook.BeforeFinalize);
        values.Should().Contain(PipelineHook.AfterFinalize);
        values.Should().HaveCount(8);
    }

    [Fact]
    public void PipelineHook_HasSymmetricBeforeAfterPairs()
    {
        PipelineHook[] values = Enum.GetValues<PipelineHook>();
        int beforeCount = values.Count(v => v.ToString().StartsWith("Before"));
        int afterCount = values.Count(v => v.ToString().StartsWith("After"));

        beforeCount.Should().Be(afterCount);
    }
}
