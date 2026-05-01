using NoMercy.Encoder.Composition;

namespace NoMercy.Tests.Encoder.Composition;

/// <summary>
/// EncoderOptions is the DI-registered config object every encoder
/// component reaches through. Two resolver properties deserve tests:
///
///   - FfmpegPath / FfprobePath throw a clear InvalidOperationException
///     when the overrides aren't set, instead of propagating a
///     cryptic "process not found" from FfmpegExecutor later.
///   - ResolvedLiveTranscodeCachePath defaults to a sane temp location
///     so live sessions never write to the repo directory by accident.
/// </summary>
public class EncoderOptionsTests
{
    [Fact]
    public void FfmpegPath_WithoutOverride_ThrowsWithActionableMessage()
    {
        EncoderOptions options = new();

        Action act = () => _ = options.FfmpegPath;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*AddNoMercyEncoder*", "message must point the caller at the DI helper");
    }

    [Fact]
    public void FfprobePath_WithoutOverride_Throws()
    {
        EncoderOptions options = new();

        Action act = () => _ = options.FfprobePath;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FfmpegPath_WithOverride_ReturnsOverride()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "/opt/ffmpeg/bin/ffmpeg" };

        options.FfmpegPath.Should().Be("/opt/ffmpeg/bin/ffmpeg");
    }

    [Fact]
    public void FfprobePath_WithOverride_ReturnsOverride()
    {
        EncoderOptions options = new() { FfprobePathOverride = "/opt/ffmpeg/bin/ffprobe" };

        options.FfprobePath.Should().Be("/opt/ffmpeg/bin/ffprobe");
    }

    [Fact]
    public void ResolvedLiveTranscodeCachePath_UnsetDefault_UsesTempDir()
    {
        EncoderOptions options = new();

        string resolved = options.ResolvedLiveTranscodeCachePath;

        resolved.Should().EndWith("nomercy-live");
        resolved.Should().StartWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ResolvedLiveTranscodeCachePath_WithOverride_UsesOverride()
    {
        EncoderOptions options = new() { LiveTranscodeCachePath = "/custom/live-cache" };

        options.ResolvedLiveTranscodeCachePath.Should().Be("/custom/live-cache");
    }

    [Fact]
    public void NotificationWebhookUrls_DefaultsToEmpty()
    {
        // Must be a mutable, initialized list — callers append URLs.
        // Null-default would NRE the first Add().
        EncoderOptions options = new();

        options.NotificationWebhookUrls.Should().NotBeNull();
        options.NotificationWebhookUrls.Should().BeEmpty();

        options.NotificationWebhookUrls.Add("https://example.com/hook");
        options.NotificationWebhookUrls.Should().ContainSingle();
    }

    [Fact]
    public void DefaultSegmentDuration_IsFourSeconds()
    {
        // 4s is the Apple HLS authoring spec default. EncoderOptions's
        // value drives Live sessions (OutputPlan has its own 6s default
        // for file encodes — different use case).
        EncoderOptions options = new();
        options.DefaultSegmentDurationSeconds.Should().Be(4);
    }

    [Fact]
    public void BufferAheadWindow_MinBelowMax()
    {
        // BufferManager relies on the invariant Min < Max — violating it
        // means the "suspend" and "resume" states overlap and the live
        // session thrashes between them.
        EncoderOptions options = new();
        options.MinBufferAheadSeconds.Should().BeLessThan(options.MaxBufferAheadSeconds);
    }

    [Fact]
    public void AutoCalibrate_DefaultsTrue()
    {
        // Fresh install should benchmark hardware on first start. Opt-out
        // for test containers / CI by setting this to false.
        EncoderOptions options = new();
        options.AutoCalibrate.Should().BeTrue();
    }
}
