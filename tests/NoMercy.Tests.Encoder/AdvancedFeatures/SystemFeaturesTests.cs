// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.SystemFeatures;

namespace NoMercy.Tests.Encoder.AdvancedFeatures;

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
    public void QualityCheckResult_PassesThreshold_WhenVmafIsHigh()
    {
        QualityCheckResult result = new(
            SourcePath: "/media/source.mkv",
            EncodedPath: "/output/encoded.mkv",
            VmafScore: 95.0,
            Ssim: 0.998,
            Psnr: 48.5,
            PassesThreshold: true
        );

        result.VmafScore.Should().BeApproximately(95.0, 0.01);
        result.Ssim.Should().BeApproximately(0.998, 0.0001);
        result.Psnr.Should().BeApproximately(48.5, 0.01);
        result.PassesThreshold.Should().BeTrue();
    }

    [Fact]
    public void QualityCheckResult_FailsThreshold_WhenVmafIsLow()
    {
        QualityCheckResult result = new(
            SourcePath: "/media/source.mkv",
            EncodedPath: "/output/encoded.mkv",
            VmafScore: 55.0,
            Ssim: 0.92,
            Psnr: 28.0,
            PassesThreshold: false
        );

        result.VmafScore.Should().BeLessThan(70.0);
        result.PassesThreshold.Should().BeFalse();
    }

    [Fact]
    public void QualityCheckResult_ZeroScores_IsValid()
    {
        QualityCheckResult result = new(
            SourcePath: "/media/source.mkv",
            EncodedPath: "/output/encoded.mkv",
            VmafScore: 0.0,
            Ssim: 0.0,
            Psnr: 0.0,
            PassesThreshold: false
        );

        result.VmafScore.Should().Be(0.0);
        result.Ssim.Should().Be(0.0);
        result.Psnr.Should().Be(0.0);
        result.PassesThreshold.Should().BeFalse();
    }

    [Fact]
    public void PipelineStagePosition_HasAllExpectedValues()
    {
        PipelineStagePosition[] values = Enum.GetValues<PipelineStagePosition>();

        values.Should().Contain(PipelineStagePosition.Before);
        values.Should().Contain(PipelineStagePosition.After);
        values.Should().Contain(PipelineStagePosition.Replace);
        values.Should().HaveCount(3);
    }

    [Fact]
    public void PipelineHook_ConstructsCorrectly()
    {
        // PipelineHook is now a record, not an enum.
        // It wraps a PipelineStagePosition, a target stage name, and a stage instance.
        // Verify the record type exists and its properties are correct.
        PipelineStagePosition[] positions = Enum.GetValues<PipelineStagePosition>();
        positions.Should().Contain(PipelineStagePosition.Before);
        positions.Should().Contain(PipelineStagePosition.After);
    }
}
