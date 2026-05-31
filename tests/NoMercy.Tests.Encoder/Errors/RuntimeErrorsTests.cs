using System.Reflection;
using NoMercy.Encoder.Errors;

namespace NoMercy.Tests.Encoder.Errors;

public class RuntimeErrorsTests
{
    [Fact]
    public void GpuCapacityExhausted_carries_409_and_catalogued_id()
    {
        EncoderRuntimeException ex = RuntimeErrors.GpuCapacityExhausted("RTX 4090", 3);

        ex.HttpStatusCode.Should().Be(409);
        ex.Shape.Id.Should().Be(EncoderRuleId.GpuCapacityExhausted);
        ex.Shape.Message.Should().Contain("RTX 4090").And.Contain("3 active");
        ex.Shape.Suggestion.Should().NotBeNullOrWhiteSpace();
        ex.Shape.Details.Should().BeOfType<RuntimeErrors.GpuCapacityDetails>();
    }

    [Fact]
    public void GpuCapacityExhausted_accepts_custom_suggestion()
    {
        EncoderRuntimeException ex = RuntimeErrors.GpuCapacityExhausted(
            "RTX 4090",
            3,
            "Wait 30s — current encode at 87%."
        );

        ex.Shape.Suggestion.Should().Be("Wait 30s — current encode at 87%.");
    }

    [Fact]
    public void EncoderInitFailed_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.EncoderInitFailed(
            "h264_nvenc",
            "driver too old"
        );

        ex.HttpStatusCode.Should().Be(500);
        ex.Shape.Id.Should().Be(EncoderRuleId.EncoderInitFailed);
        ex.Shape.Message.Should().Contain("h264_nvenc").And.Contain("driver too old");
    }

    [Fact]
    public void SourceNotAccessible_returns_404()
    {
        EncoderRuntimeException ex = RuntimeErrors.SourceNotAccessible("/movies/missing.mkv");

        ex.HttpStatusCode.Should().Be(404);
        ex.Shape.Id.Should().Be(EncoderRuleId.SourceNotAccessible);
    }

    [Fact]
    public void SourceReadError_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.SourceReadError("/movies/x.mkv", "EIO");

        ex.HttpStatusCode.Should().Be(500);
        ex.Shape.Id.Should().Be(EncoderRuleId.SourceReadError);
    }

    [Fact]
    public void OutputWriteError_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputWriteError("/out/v.m3u8", "ENOSPC");

        ex.HttpStatusCode.Should().Be(500);
        ex.Shape.Id.Should().Be(EncoderRuleId.OutputWriteError);
    }

    [Fact]
    public void OutputPathNotAllowed_returns_403_and_includes_reason()
    {
        EncoderRuntimeException ex = RuntimeErrors.OutputPathNotAllowed(
            "/etc/passwd",
            "path is not under any allowed root"
        );

        ex.HttpStatusCode.Should().Be(403);
        ex.Shape.Id.Should().Be(EncoderRuleId.OutputPathNotAllowed);
        ex.Shape.Message.Should().Contain("/etc/passwd");
    }

    [Fact]
    public void LicenseRevoked_returns_403()
    {
        EncoderRuntimeException ex = RuntimeErrors.LicenseRevoked("subscription expired");

        ex.HttpStatusCode.Should().Be(403);
        ex.Shape.Id.Should().Be(EncoderRuleId.LicenseRevoked);
    }

    [Fact]
    public void LicenseUnreachable_returns_503()
    {
        EncoderRuntimeException ex = RuntimeErrors.LicenseUnreachable("https://api.nomercy.tv");

        ex.HttpStatusCode.Should().Be(503);
        ex.Shape.Id.Should().Be(EncoderRuleId.LicenseUnreachable);
    }

    [Fact]
    public void HardwareForcedButUnavailable_returns_422()
    {
        EncoderRuntimeException ex = RuntimeErrors.HardwareForcedButUnavailable("h264_nvenc");

        ex.HttpStatusCode.Should().Be(422);
        ex.Shape.Id.Should().Be(EncoderRuleId.HardwareForcedButUnavailable);
        ex.Shape.Message.Should().Contain("h264_nvenc");
    }

    [Fact]
    public void JobInterruptedNoCheckpoint_returns_500()
    {
        EncoderRuntimeException ex = RuntimeErrors.JobInterruptedNoCheckpoint("job-123");

        ex.HttpStatusCode.Should().Be(500);
        ex.Shape.Id.Should().Be(EncoderRuleId.JobInterruptedNoCheckpoint);
    }

    [Fact]
    public void DiscDriveBusy_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscDriveBusy("/dev/sr0");

        ex.HttpStatusCode.Should().Be(409);
        ex.Shape.Id.Should().Be(EncoderRuleId.DiscDriveBusy);
    }

    [Fact]
    public void DiscAacsCertMissing_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscAacsCertMissing("VOL-123");

        ex.HttpStatusCode.Should().Be(409);
        ex.Shape.Id.Should().Be(EncoderRuleId.DiscAacsCertMissing);
    }

    [Fact]
    public void DiscBdplusConverterMissing_returns_409()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscBdplusConverterMissing("VOL-456");

        ex.HttpStatusCode.Should().Be(409);
        ex.Shape.Id.Should().Be(EncoderRuleId.DiscBdplusConverterMissing);
    }

    [Fact]
    public void DiscReadError_returns_500_with_stderr_tail()
    {
        EncoderRuntimeException ex = RuntimeErrors.DiscReadError(
            "/dev/sr0",
            "I/O error at sector 12345"
        );

        ex.HttpStatusCode.Should().Be(500);
        ex.Shape.Id.Should().Be(EncoderRuleId.DiscReadError);
        ex.Shape.Details.Should().BeOfType<RuntimeErrors.DiscReadDetails>();
    }

    [Fact]
    public void DistributionHmacInvalid_returns_401()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionHmacInvalid();

        ex.HttpStatusCode.Should().Be(401);
        ex.Shape.Id.Should().Be(EncoderRuleId.DistributionHmacInvalid);
    }

    [Fact]
    public void DistributionTimestampReplay_returns_401()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionTimestampReplay(900);

        ex.HttpStatusCode.Should().Be(401);
        ex.Shape.Id.Should().Be(EncoderRuleId.DistributionTimestampReplay);
        ex.Shape.Message.Should().Contain("900s");
    }

    [Fact]
    public void DistributionWorkerNotRegistered_returns_404()
    {
        EncoderRuntimeException ex = RuntimeErrors.DistributionWorkerNotRegistered(
            "worker-eagle-1"
        );

        ex.HttpStatusCode.Should().Be(404);
        ex.Shape.Id.Should().Be(EncoderRuleId.DistributionWorkerNotRegistered);
    }

    [Fact]
    public void Every_factory_emits_a_catalogued_id()
    {
        EncoderRuntimeException[] all =
        [
            RuntimeErrors.GpuCapacityExhausted("g", 1),
            RuntimeErrors.EncoderInitFailed("h", "r"),
            RuntimeErrors.SourceNotAccessible("p"),
            RuntimeErrors.SourceReadError("p", "d"),
            RuntimeErrors.OutputWriteError("p", "d"),
            RuntimeErrors.OutputPathNotAllowed("p", "r"),
            RuntimeErrors.LicenseRevoked("r"),
            RuntimeErrors.LicenseUnreachable("u"),
            RuntimeErrors.HardwareForcedButUnavailable("h"),
            RuntimeErrors.JobInterruptedNoCheckpoint("j"),
            RuntimeErrors.DiscDriveBusy("/dev/sr0"),
            RuntimeErrors.DiscAacsCertMissing("v"),
            RuntimeErrors.DiscBdplusConverterMissing("v"),
            RuntimeErrors.DiscReadError("d", "s"),
            RuntimeErrors.DistributionHmacInvalid(),
            RuntimeErrors.DistributionTimestampReplay(1),
            RuntimeErrors.DistributionWorkerNotRegistered("w"),
        ];

        // Reflect over EncoderRuleId to confirm every emitted ID exists in the catalogue.
        IEnumerable<string> catalogued = typeof(EncoderRuleId)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (string)f.GetValue(null)!);

        foreach (EncoderRuntimeException ex in all)
            catalogued.Should().Contain(ex.Shape.Id, $"{ex.Shape.Id} must be in EncoderRuleId");
    }
}
