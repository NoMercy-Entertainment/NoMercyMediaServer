using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Subscribers;

namespace NoMercy.Tests.Encoder.Subscribers;

/// <summary>
/// CropDetectSubscriber is an opt-out toggle — PlanStage reads
/// <see cref="CropDetectSubscriber.IsEnabled"/> alongside the per-profile
/// AutoDetectCrop flag to decide whether to actually invoke ICropDetector.
/// False here means the detector is globally disabled regardless of profile.
/// </summary>
public class CropDetectSubscriberTests
{
    [Fact]
    public void IsEnabled_DefaultsToTrueFromOptions()
    {
        EncoderOptions options = new();
        // Sanity: encoder ships with crop detect on by default.
        options.EnableCropDetectSubscriber.Should().BeTrue();

        CropDetectSubscriber subject = new(options, NullLogger<CropDetectSubscriber>.Instance);

        subject.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_FollowsOptionsToggle()
    {
        EncoderOptions options = new() { EnableCropDetectSubscriber = false };

        CropDetectSubscriber subject = new(options, NullLogger<CropDetectSubscriber>.Instance);

        subject.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DoesNotThrow_WhenDisabled()
    {
        // Logging on construction when disabled is a side-effect we don't
        // want crashing the host. Verify construction never throws either way.
        EncoderOptions options = new() { EnableCropDetectSubscriber = false };

        Action act = () =>
            _ = new CropDetectSubscriber(options, NullLogger<CropDetectSubscriber>.Instance);

        act.Should().NotThrow();
    }
}
