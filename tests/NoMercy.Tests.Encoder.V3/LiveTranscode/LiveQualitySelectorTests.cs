namespace NoMercy.Tests.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Codecs;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.LiveTranscode;

public class LiveQualitySelectorTests
{
    private readonly LiveQualitySelector _selector = new(
        new CodecRegistry(),
        new CodecResolver(new CodecRegistry())
    );

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static MediaInfo MakeMedia(int width, int height) =>
        new(
            FilePath: "/media/test.mkv",
            Format: "matroska,webm",
            Duration: TimeSpan.FromMinutes(90),
            OverallBitRateKbps: 15000,
            FileSizeBytes: 10_000_000_000L,
            VideoStreams:
            [
                new VideoStreamInfo(
                    Index: 0,
                    Codec: "h264",
                    Width: width,
                    Height: height,
                    FrameRate: 24.0,
                    BitDepth: 8,
                    PixelFormat: "yuv420p",
                    ColorPrimaries: "bt709",
                    ColorTransfer: "bt709",
                    ColorSpace: "bt709",
                    IsDefault: true,
                    BitRateKbps: 15000
                ),
            ],
            AudioStreams: [],
            SubtitleStreams: [],
            Chapters: []
        );

    private static ClientCapabilities MakeClient(int maxWidth = 7680, int maxHeight = 4320) =>
        new(
            SupportedVideoCodecs: [VideoCodecType.H264, VideoCodecType.H265],
            SupportedAudioCodecs: [AudioCodecType.Aac],
            SupportedContainers: ["mp4", "mkv"],
            MaxWidth: maxWidth,
            MaxHeight: maxHeight,
            SupportsHdr: false,
            Supports10Bit: false,
            MaxBitrateKbps: 0
        );

    private static IHardwareCapabilities MakeGpuHardware() =>
        new HardwareCapabilities(
            Gpus:
            [
                new GpuDevice(
                    Vendor: GpuVendor.Nvidia,
                    Name: "RTX 4090",
                    VramMb: 24576,
                    MaxEncoderSessions: 12,
                    SupportedCodecs: [VideoCodecType.H264, VideoCodecType.H265, VideoCodecType.Av1]
                ),
            ],
            CpuCores: 16
        );

    private static IHardwareCapabilities MakeSoftwareHardware() =>
        new HardwareCapabilities(Gpus: [], CpuCores: 8);

    private static SpeedIndex MakeFastGpuSpeedIndex() =>
        new(
            new Dictionary<SpeedKey, SpeedMeasurement>
            {
                // Client supports H264+H265; selector prefers H265 → resolves hevc_nvenc on NVIDIA
                [new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 3840, "RTX 4090")] = new(
                    100.0,
                    4.0,
                    DateTime.UtcNow
                ),
                [new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1920, "RTX 4090")] = new(
                    180.0,
                    7.5,
                    DateTime.UtcNow
                ),
                [new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 1280, "RTX 4090")] = new(
                    240.0,
                    10.0,
                    DateTime.UtcNow
                ),
                [new SpeedKey(VideoCodecType.H265, "hevc_nvenc", 854, "RTX 4090")] = new(
                    300.0,
                    12.5,
                    DateTime.UtcNow
                ),
            }
        );

    private static SpeedIndex MakeEmptySpeedIndex() =>
        new(new Dictionary<SpeedKey, SpeedMeasurement>());

    // ──────────────────────────────────────────────────────────────────────────
    // GetAvailableQualities
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FourK_Input_FastGpu_ProducesQualities_IncludingFourK()
    {
        MediaInfo media = MakeMedia(3840, 2160);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeFastGpuSpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality[] qualities = _selector.GetAvailableQualities(media, client, speeds, hardware);

        qualities.Should().NotBeEmpty();
        qualities.Should().Contain(q => q.Width == 3840 && q.Height == 2160);
    }

    [Fact]
    public void FourK_Input_SkipsResolutionsLargerThanSource()
    {
        MediaInfo media = MakeMedia(1280, 720);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeFastGpuSpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality[] qualities = _selector.GetAvailableQualities(media, client, speeds, hardware);

        qualities.Should().NotContain(q => q.Width > 1280);
    }

    [Fact]
    public void NoSpeedData_AllMarkedCanRealtimeFalse()
    {
        MediaInfo media = MakeMedia(1920, 1080);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeEmptySpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality[] qualities = _selector.GetAvailableQualities(media, client, speeds, hardware);

        qualities.Should().NotBeEmpty();
        qualities.Should().OnlyContain(q => q.CanRealtime == false);
    }

    [Fact]
    public void FastGpu_HighSpeedMultiplier_MarksCanRealtimeTrue()
    {
        MediaInfo media = MakeMedia(1920, 1080);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeFastGpuSpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality[] qualities = _selector.GetAvailableQualities(media, client, speeds, hardware);

        qualities.Should().Contain(q => q.CanRealtime);
    }

    [Fact]
    public void SoftwareOnly_IsHardwareAcceleratedFalse()
    {
        MediaInfo media = MakeMedia(1920, 1080);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeEmptySpeedIndex();
        IHardwareCapabilities hardware = MakeSoftwareHardware();

        LiveQuality[] qualities = _selector.GetAvailableQualities(media, client, speeds, hardware);

        qualities.Should().NotBeEmpty();
        qualities.Should().OnlyContain(q => q.IsHardwareAccelerated == false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SelectOptimal
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FourK_FastGpu_SelectsHighestCanRealtime()
    {
        MediaInfo media = MakeMedia(3840, 2160);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeFastGpuSpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality optimal = _selector.SelectOptimal(media, client, speeds, hardware);

        optimal.CanRealtime.Should().BeTrue();
        optimal.Width.Should().Be(3840);
    }

    [Fact]
    public void NoSpeedData_FallsBackToLowestQuality()
    {
        MediaInfo media = MakeMedia(1920, 1080);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeEmptySpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality optimal = _selector.SelectOptimal(media, client, speeds, hardware);

        // No CanRealtime candidates → falls back to lowest resolution tier
        optimal.Should().NotBeNull();
        optimal.Width.Should().BeLessThanOrEqualTo(1920);
    }

    [Fact]
    public void Client_Max720p_CapsOutputAt720p()
    {
        MediaInfo media = MakeMedia(1920, 1080);
        ClientCapabilities client = MakeClient(maxWidth: 1280, maxHeight: 720);
        SpeedIndex speeds = MakeFastGpuSpeedIndex();
        IHardwareCapabilities hardware = MakeGpuHardware();

        LiveQuality optimal = _selector.SelectOptimal(media, client, speeds, hardware);

        optimal.Width.Should().BeLessThanOrEqualTo(1280);
        optimal.Height.Should().BeLessThanOrEqualTo(720);
    }

    [Fact]
    public void SoftwareOnly_MarkedNotHardwareAccelerated()
    {
        MediaInfo media = MakeMedia(1280, 720);
        ClientCapabilities client = MakeClient();
        SpeedIndex speeds = MakeEmptySpeedIndex();
        IHardwareCapabilities hardware = MakeSoftwareHardware();

        LiveQuality optimal = _selector.SelectOptimal(media, client, speeds, hardware);

        optimal.IsHardwareAccelerated.Should().BeFalse();
    }
}
