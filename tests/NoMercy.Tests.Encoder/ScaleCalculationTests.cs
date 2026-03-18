using NoMercy.Encoder.Commands;
using NoMercy.Encoder.Format.Audio;
using NoMercy.Encoder.Format.Container;
using NoMercy.Encoder.Format.Rules;
using NoMercy.Encoder.Format.Video;
using NoMercy.NmSystem;
using ScaleArea = NoMercy.Encoder.Format.Rules.Classes.ScaleArea;

namespace NoMercy.Tests.Encoder;

/// <summary>
/// Tests for aspect ratio and scale calculation correctness in the encoder.
///
/// Bugs fixed:
///   1. Classes.AspectRatioValue returned NaN when no crop was set (0.0/0.0).
///      The NaN propagated into the Scale getter, producing H=0 for any stream
///      using SetScale(int) / "width:-2" notation — worst at 480p.
///   2. FFmpegCommandBuilder.ApplyFinalScaleAdjustments mutated stream.Scale.W/H
///      directly. The Scale getter returns a new ScaleArea object on each call so
///      those writes were silently discarded and ScaleValue (the backing string
///      passed to FFmpeg) was never updated.
/// </summary>
[Trait("Category", "Unit")]
public class ScaleCalculationTests : IDisposable
{
    private readonly string _tempDir;

    public ScaleCalculationTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "NoMercy_ScaleTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region Helpers

    private static FfProbeData BuildProbeData(int width, int height,
        string pixFmt = "yuv420p", string colorSpace = "bt709")
    {
        FfProbeVideoStream vs = new()
        {
            Width = width,
            Height = height,
            PixFmt = pixFmt,
            ColorSpace = colorSpace,
            Index = 0,
            CodecName = "h264"
        };

        FfProbeAudioStream audio = new()
        {
            Language = "eng",
            Channels = 2,
            SampleRate = 48000,
            BitRate = 128000,
            Index = 1,
            CodecName = "aac"
        };

        return new()
        {
            FilePath = "/input/test.mkv",
            Duration = TimeSpan.FromMinutes(90),
            VideoStreams = [vs],
            AudioStreams = [audio],
            PrimaryVideoStream = vs,
            PrimaryAudioStream = audio
        };
    }

    private Hls BuildHls(FfProbeData probe, BaseVideo video, BaseAudio audioCodec)
    {
        Hls hls = new();
        hls.InputFile = probe.FilePath;
        hls.FfProbeData = probe;
        hls.Title = "Test";
        hls.BasePath = _tempDir;
        hls.FileName = "playlist";
        hls.IsVideo = true;

        video.VideoStreams = probe.VideoStreams;
        video.VideoStream = probe.PrimaryVideoStream;
        video.Index = probe.PrimaryVideoStream!.Index;
        video.Title = "Test";
        video.Container = hls;
        video.FileName = "playlist";
        video.BasePath = _tempDir;
        BaseVideo built = video.Build();
        built.ApplyFlags();
        hls.VideoStreams.Add(built);

        audioCodec.AudioStreams = probe.AudioStreams;
        audioCodec.AudioStream = probe.PrimaryAudioStream!;
        audioCodec.IsAudio = true;
        audioCodec.FileName = "playlist";
        audioCodec.BasePath = _tempDir;
        List<BaseAudio> builtAudio = audioCodec.Build();
        foreach (BaseAudio a in builtAudio)
            a.Extension = hls.Extension;
        hls.AudioStreams.AddRange(builtAudio);

        return hls;
    }

    private static string BuildCommand(BaseContainer container, FfProbeData probe)
    {
        FFmpegCommandBuilder builder = new(
            container: container,
            ffProbeData: probe,
            accelerators: [],
            priority: false);
        return builder.BuildCommand();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────────
    // AspectRatioValue — unit tests directly on the Classes property
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AspectRatioValue_NoCrop_ReturnsZero_NotNaN()
    {
        // Before the fix: Crop.H / Crop.W was 0.0 / 0.0 = NaN.
        // After the fix: guard returns 0.0 so callers can detect "not set".
        X264 stream = new();
        // No CropValue set — Crop.W and Crop.H are both 0.
        double result = stream.AspectRatioValue;

        Assert.False(double.IsNaN(result), "AspectRatioValue must not be NaN when no crop is set");
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void AspectRatioValue_StandardCrop_ReturnsCorrectRatio()
    {
        X264 stream = new();
        // Simulate a letterboxed 1920x800 crop on a 1920x1080 source.
        stream.CropValue = "1920:800:0:140";

        double result = stream.AspectRatioValue;

        // Expected: 800 / 1920 ≈ 0.4167
        Assert.False(double.IsNaN(result));
        Assert.InRange(result, 0.416, 0.418);
    }

    [Fact]
    public void AspectRatioValue_SquareCrop_ReturnsOne()
    {
        X264 stream = new();
        stream.CropValue = "500:500:0:0";

        double result = stream.AspectRatioValue;

        Assert.Equal(1.0, result, precision: 6);
    }

    [Fact]
    public void AspectRatioValue_VerticalCrop_ReturnsRatioGreaterThanOne()
    {
        X264 stream = new();
        stream.CropValue = "720:1280:0:0";

        double result = stream.AspectRatioValue;

        // 1280 / 720 ≈ 1.778
        Assert.InRange(result, 1.77, 1.78);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scale getter — unit tests on the Classes.Scale property
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scale_ExplicitWidthHeight_ReturnsCorrectDimensions()
    {
        X264 stream = new();
        stream.SetScale(1920, 1080);

        ScaleArea scale = stream.Scale;

        Assert.Equal(1920, scale.W);
        Assert.Equal(1080, scale.H);
    }

    [Fact]
    public void Scale_AutoHeight_NoCrop_RetainsMinusTwoSentinel()
    {
        // When no crop is set and SetScale(int) is called, ScaleValue is "width:-2".
        // AspectRatioValue is 0, so the getter cannot resolve -2 to pixels.
        // The -2 should be returned as-is; the FFmpegCommandBuilder resolves it.
        X264 stream = new();
        stream.SetScale(854); // stores "854:-2"

        ScaleArea scale = stream.Scale;

        Assert.Equal(854, scale.W);
        Assert.Equal(-2, scale.H);
    }

    [Fact]
    public void Scale_AutoHeight_WithCrop_ComputesHeightFromCropRatio()
    {
        // When a crop is set, the -2 should be resolved using the crop aspect ratio.
        X264 stream = new();
        stream.CropValue = "1920:800:0:140"; // letterbox, ratio = 800/1920
        stream.SetScale(854); // stores "854:-2"

        ScaleArea scale = stream.Scale;

        Assert.Equal(854, scale.W);
        // 854 * (800/1920) ≈ 355.8 → 355
        Assert.Equal((int)(854 * (800.0 / 1920.0)), scale.H);
        Assert.True(scale.H > 0, "Computed height must be positive");
    }

    [Fact]
    public void Scale_EmptyScaleValue_ReturnsBothZero()
    {
        X264 stream = new();
        // ScaleValue is "" by default

        ScaleArea scale = stream.Scale;

        Assert.Equal(0, scale.W);
        Assert.Equal(0, scale.H);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scale setter — round-trip through ScaleValue
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Scale_Setter_UpdatesScaleValue()
    {
        X264 stream = new();
        stream.Scale = new() { W = 1280, H = 720 };

        Assert.Equal("1280:720", stream.ScaleValue);
        Assert.Equal(1280, stream.Scale.W);
        Assert.Equal(720, stream.Scale.H);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FFmpegCommandBuilder — scale in the generated command
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildCommand_480p_NoCrop_ScaleValueInCommand_NotZeroHeight()
    {
        // This is the specific regression from the bug report.
        // 480p with no crop used to produce scale=854:0 in the FFmpeg command.
        FfProbeData probe = BuildProbeData(1920, 1080);
        X264 video = new();
        video.SetScale(854, 480);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_854x480");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        // Must contain a valid scale value — never scale=854:0
        Assert.DoesNotContain("scale=854:0", command);
        Assert.Contains("scale=854:480", command);
    }

    [Fact]
    public void BuildCommand_480p_AutoHeight_NoCrop_ProducesNonZeroScaleInCommand()
    {
        // SetScale(int) stores "854:-2". Without a crop, AspectRatioValue is 0
        // and the Scale getter returns H=-2. The FFmpegCommandBuilder must resolve
        // this to real pixel dimensions before writing the filter_complex string.
        FfProbeData probe = BuildProbeData(1920, 1080);
        X264 video = new();
        video.SetScale(854); // stores "854:-2"
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_854x480");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        // The builder resolves -2 to the source height (1080 for a 1920x1080 source
        // scaled to width 854, but the upscale guard then clamps to 1920x1080).
        // The key property: scale height in the command must not be 0 or -2.
        Assert.DoesNotContain("scale=854:0", command);
        Assert.DoesNotContain("scale=854:-2", command);
    }

    [Fact]
    public void BuildCommand_720p_ExplicitDimensions_ScaleInCommandIsCorrect()
    {
        FfProbeData probe = BuildProbeData(1920, 1080);
        X264 video = new();
        video.SetScale(1280, 720);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1280x720");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("scale=1280:720", command);
    }

    [Fact]
    public void BuildCommand_1080p_ExplicitDimensions_ScaleInCommandIsCorrect()
    {
        FfProbeData probe = BuildProbeData(1920, 1080);
        X264 video = new();
        video.SetScale(1920, 1080);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("scale=1920:1080", command);
    }

    [Fact]
    public void BuildCommand_SquareVideo_480p_ScaleIsCorrect()
    {
        // Edge case: 1:1 aspect ratio source
        FfProbeData probe = BuildProbeData(1080, 1080);
        X264 video = new();
        video.SetScale(480, 480);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_480x480");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("scale=480:480", command);
    }

    [Fact]
    public void BuildCommand_UltrawideSource_ScaleDimensionsAreNonZero()
    {
        // Edge case: 21:9 ultrawide (2560x1080)
        FfProbeData probe = BuildProbeData(2560, 1080);
        X264 video = new();
        video.SetScale(1280, 540);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1280x540");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("scale=1280:540", command);
    }

    [Fact]
    public void BuildCommand_VerticalVideo_ScaleDimensionsAreNonZero()
    {
        // Edge case: vertical video (portrait 1080x1920)
        FfProbeData probe = BuildProbeData(1080, 1920);
        X264 video = new();
        video.SetScale(540, 960);
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_540x960");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.Contains("scale=540:960", command);
    }

    [Fact]
    public void BuildCommand_WithCrop_480p_AutoHeight_ProducesPositiveHeight()
    {
        // When crop is set and SetScale(int) is used, the getter resolves -2 via
        // crop aspect ratio and the command must have a positive height.
        FfProbeData probe = BuildProbeData(1920, 1080);
        X264 video = new();
        video.CropValue = "1920:800:0:140"; // letterbox crop
        video.SetScale(854); // "854:-2" — resolved to 854 * (800/1920) ≈ 355
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_854x355");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        Assert.DoesNotContain("scale=854:0", command);
        Assert.DoesNotContain("scale=854:-2", command);
        // Height computed from crop: (int)(854 * 800.0/1920) = 355
        Assert.Contains("scale=854:355", command);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ApplyFinalScaleAdjustments — ScaleValue round-trip after mutation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildCommand_ScaleValueReflectsOverrideWhenTargetExceedsSource()
    {
        // When the requested scale exceeds the source dimensions, the builder must
        // clamp to source dimensions AND update ScaleValue so the filter_complex
        // string uses the clamped values, not the original oversized values.
        FfProbeData probe = BuildProbeData(1280, 720);
        X264 video = new();
        video.SetScale(1920, 1080); // bigger than the 1280x720 source
        video.SetColorSpace(VideoPixelFormats.Yuv420P);
        video.SetHlsPlaylistFilename("video_1920x1080");

        Aac audio = new();
        audio.SetHlsPlaylistFilename("audio_eng");
        Hls hls = BuildHls(probe, video, audio);

        string command = BuildCommand(hls, probe);

        // Must be clamped to source dimensions, not the oversized requested values
        Assert.DoesNotContain("scale=1920:1080", command);
        Assert.Contains("scale=1280:720", command);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NormalizeAspectRatio — sanity checks on the helper used for metadata
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1920, 1080, "16:9")]
    [InlineData(1280, 720, "16:9")]
    [InlineData(3840, 2160, "16:9")]
    [InlineData(640, 480, "4:3")]
    [InlineData(1024, 768, "4:3")]
    [InlineData(1080, 1080, "1:1")]
    public void NormalizeAspectRatio_CommonResolutions_ReturnExpectedRatioString(
        int width, int height, string expected)
    {
        string result = NoMercy.NmSystem.Extensions.NumberConverter.NormalizeAspectRatio(width, height);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeAspectRatio_ZeroDimensions_ReturnsFallback()
    {
        string result = NoMercy.NmSystem.Extensions.NumberConverter.NormalizeAspectRatio(0, 0);
        Assert.Equal("1:1", result);
    }
}
