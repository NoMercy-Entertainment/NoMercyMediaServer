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

using System.Reflection;
using NoMercy.Encoder.Errors;

namespace NoMercy.Tests.Encoder.Errors;

public class RuntimeErrorsTests
{
    [Fact]
    public void GpuCapacityExhausted_carries_409_and_catalogued_id()
    {
        EncoderRuntimeException ex = RuntimeErrors.GpuCapacityExhausted(gpu: "RTX 4090", sessions: 3);

        ex.HttpStatusCode.Should().Be(expected: 409);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.GpuCapacityExhausted);
        ex.Shape.Message.Should().Contain(expected: "RTX 4090").And.Contain(expected: "3 active");
        ex.Shape.Suggestion.Should().NotBeNullOrWhiteSpace();
        ex.Shape.Details.Should().BeOfType<RuntimeErrors.GpuCapacityDetails>();
    }

    [Fact]
    public void GpuCapacityExhausted_accepts_custom_suggestion()
    {
        EncoderRuntimeException ex = RuntimeErrors.GpuCapacityExhausted(
            gpu: "RTX 4090",
            sessions: 3,
            suggestion: "Wait 30s — current encode at 87%."
        );

        ex.Shape.Suggestion.Should().Be(expected: "Wait 30s — current encode at 87%.");
    }

    [Fact]
    public void EncoderInitFailed_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.EncoderInitFailed(
            handle: "h264_nvenc",
            reason: "driver too old"
        );

        ex.HttpStatusCode.Should().Be(expected: 500);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.EncoderInitFailed);
        ex.Shape.Message.Should().Contain(expected: "h264_nvenc").And.Contain(expected: "driver too old");
    }

    [Fact]
    public void SourceNotAccessible_returns_404()
    {
        EncoderRuntimeException ex = RuntimeErrors.SourceNotAccessible(path: "/movies/missing.mkv");

        ex.HttpStatusCode.Should().Be(expected: 404);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.SourceNotAccessible);
    }

    [Fact]
    public void SourceReadError_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.SourceReadError(path: "/movies/x.mkv", detail: "EIO");

        ex.HttpStatusCode.Should().Be(expected: 500);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.SourceReadError);
    }

    [Fact]
    public void OutputWriteError_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputWriteError(path: "/out/v.m3u8", detail: "ENOSPC");

        ex.HttpStatusCode.Should().Be(expected: 500);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.OutputWriteError);
    }

    [Fact]
    public void OutputPathNotAllowed_returns_403_and_includes_reason()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputPathNotAllowed(
            path: "/etc/passwd",
            reason: "path is not under any allowed root"
        );

        ex.HttpStatusCode.Should().Be(expected: 403);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.OutputPathNotAllowed);
        ex.Shape.Message.Should().Contain(expected: "/etc/passwd");
    }

    [Fact]
    public void LicenseRevoked_returns_403()
    {
        EncoderRuntimeException ex = RuntimeErrors.LicenseRevoked(reason: "subscription expired");

        ex.HttpStatusCode.Should().Be(expected: 403);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.LicenseRevoked);
    }

    [Fact]
    public void LicenseUnreachable_returns_503()
    {
        EncoderRuntimeException ex = RuntimeErrors.LicenseUnreachable(url: "https://api.nomercy.tv");

        ex.HttpStatusCode.Should().Be(expected: 503);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.LicenseUnreachable);
    }

    [Fact]
    public void HardwareForcedButUnavailable_returns_422()
    {
        EncoderRuntimeException ex = RuntimeErrors.HardwareForcedButUnavailable(requested: "h264_nvenc");

        ex.HttpStatusCode.Should().Be(expected: 422);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.HardwareForcedButUnavailable);
        ex.Shape.Message.Should().Contain(expected: "h264_nvenc");
    }

    [Fact]
    public void JobInterruptedNoCheckpoint_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.JobInterruptedNoCheckpoint(jobId: "job-123");

        ex.HttpStatusCode.Should().Be(expected: 500);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.JobInterruptedNoCheckpoint);
    }

    [Fact]
    public void DiscDriveBusy_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscDriveBusy(drivePath: "/dev/sr0");

        ex.HttpStatusCode.Should().Be(expected: 409);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscDriveBusy);
    }

    [Fact]
    public void DiscAacsCertMissing_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscAacsCertMissing(volumeId: "VOL-123");

        ex.HttpStatusCode.Should().Be(expected: 409);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscAacsCertMissing);
    }

    [Fact]
    public void DiscBdplusConverterMissing_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscBdplusConverterMissing(volumeId: "VOL-456");

        ex.HttpStatusCode.Should().Be(expected: 409);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscBdplusConverterMissing);
    }

    [Fact]
    public void DiscReadError_returns_500_with_stderr_tail()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscReadError(
            drivePath: "/dev/sr0",
            ffmpegStderrTail: "I/O error at sector 12345"
        );

        ex.HttpStatusCode.Should().Be(expected: 500);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DiscReadError);
        ex.Shape.Details.Should().BeOfType<RuntimeErrors.DiscReadDetails>();
    }

    [Fact]
    public void DistributionHmacInvalid_returns_401()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionHmacInvalid();

        ex.HttpStatusCode.Should().Be(expected: 401);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DistributionHmacInvalid);
    }

    [Fact]
    public void DistributionTimestampReplay_returns_401()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionTimestampReplay(ageSeconds: 900);

        ex.HttpStatusCode.Should().Be(expected: 401);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DistributionTimestampReplay);
        ex.Shape.Message.Should().Contain(expected: "900s");
    }

    [Fact]
    public void DistributionWorkerNotRegistered_returns_404()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionWorkerNotRegistered(
            workerId: "worker-eagle-1"
        );

        ex.HttpStatusCode.Should().Be(expected: 404);
        ex.Shape.Id.Should().Be(expected: EncoderRuleId.DistributionWorkerNotRegistered);
    }

    [Fact]
    public void Every_factory_emits_a_catalogued_id()
    {
        EncoderRuntimeException[] all =
        [
            RuntimeErrors.GpuCapacityExhausted(gpu: "g", sessions: 1),
            RuntimeErrors.EncoderInitFailed(handle: "h", reason: "r"),
            RuntimeErrors.SourceNotAccessible(path: "p"),
            RuntimeErrors.SourceReadError(path: "p", detail: "d"),
            RuntimeErrors.OutputWriteError(path: "p", detail: "d"),
            RuntimeErrors.OutputPathNotAllowed(path: "p", reason: "r"),
            RuntimeErrors.LicenseRevoked(reason: "r"),
            RuntimeErrors.LicenseUnreachable(url: "u"),
            RuntimeErrors.HardwareForcedButUnavailable(requested: "h"),
            RuntimeErrors.JobInterruptedNoCheckpoint(jobId: "j"),
            RuntimeErrors.DiscDriveBusy(drivePath: "/dev/sr0"),
            RuntimeErrors.DiscAacsCertMissing(volumeId: "v"),
            RuntimeErrors.DiscBdplusConverterMissing(volumeId: "v"),
            RuntimeErrors.DiscReadError(drivePath: "d", ffmpegStderrTail: "s"),
            RuntimeErrors.DistributionHmacInvalid(),
            RuntimeErrors.DistributionTimestampReplay(ageSeconds: 1),
            RuntimeErrors.DistributionWorkerNotRegistered(workerId: "w"),
        ];

        // Reflect over EncoderRuleId to confirm every emitted ID exists in the catalogue.
        IEnumerable<string> catalogued = typeof(EncoderRuleId)
            .GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static)
            .Select(selector: f => (string)f.GetValue(obj: null)!);

        foreach (EncoderRuntimeException ex in all)
            catalogued.Should().Contain(expected: ex.Shape.Id, because: $"{ex.Shape.Id} must be in EncoderRuleId");
    }
}
