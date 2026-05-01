using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Profiles;

namespace NoMercy.Tests.Encoder.Profiles;

/// <summary>
/// MP4 is a single-file container — users who configure a multi-variant
/// ladder with OutputFormat.Mp4 would see only the first variant in the
/// output file and wonder where the rest went. The validator surfaces that
/// as a warning pointing them toward HLS or DASH for adaptive ladders.
/// </summary>
public class Mp4MultiVariantWarningTests
{
    private readonly ProfileValidator _validator = new(new());

    [Fact]
    public void Mp4_SingleVariant_NoWarning()
    {
        EncodingProfile profile = BuildProfile(variantCount: 1);

        ValidationResult result = _validator.Validate(profile);

        result.IsValid.Should().BeTrue();
        result
            .Errors.Should()
            .NotContain(e => e.Field == "VideoOutputs" && e.Message.Contains("single-file"));
    }

    [Fact]
    public void Mp4_TwoVariants_EmitsWarningPointingToHlsOrDash()
    {
        EncodingProfile profile = BuildProfile(variantCount: 2);

        ValidationResult result = _validator.Validate(profile);

        // Warnings don't fail validation — the user's encode still runs
        // (picking variant[0]) so existing profiles don't start breaking.
        result.IsValid.Should().BeTrue();
        result
            .Errors.Should()
            .Contain(e =>
                e.Field == "VideoOutputs"
                && e.Severity == ValidationSeverity.Warning
                && (e.Message.Contains("HLS") || e.Message.Contains("DASH"))
            );
    }

    [Fact]
    public void Hls_MultiVariant_NoWarning()
    {
        // HLS is the right place for multi-variant ladders — no warning.
        EncodingProfile profile = BuildProfile(variantCount: 3, format: OutputFormat.Hls);

        ValidationResult result = _validator.Validate(profile);

        result.Errors.Should().NotContain(e => e.Message.Contains("single-file"));
    }

    private static EncodingProfile BuildProfile(int variantCount, OutputFormat? format = null)
    {
        VideoOutput[] videos = Enumerable
            .Range(0, variantCount)
            .Select(i => new VideoOutput(
                Codec: VideoCodecType.H264,
                Width: 1920 >> i,
                Height: 1080 >> i,
                BitrateKbps: 4000 >> i,
                Crf: 23,
                Preset: "medium",
                Profile: "high",
                Level: "4.1",
                ConvertHdrToSdr: false,
                KeyframeIntervalSeconds: 2,
                TenBit: false
            ))
            .ToArray();

        AudioOutput audio = new(
            Codec: AudioCodecType.Aac,
            BitrateKbps: 192,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["en"]
        );

        return new(
            Id: Ulid.NewUlid(),
            Name: "Test",
            Format: format ?? OutputFormat.Mp4,
            VideoOutputs: videos,
            AudioOutputs: [audio],
            SubtitleOutputs: []
        );
    }
}
