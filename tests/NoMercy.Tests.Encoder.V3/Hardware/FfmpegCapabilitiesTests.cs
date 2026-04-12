namespace NoMercy.Tests.Encoder.V3.Hardware;

using Moq;
using NoMercy.Encoder.V3.Hardware;
using NoMercy.Encoder.V3.Infrastructure;

public class FfmpegCapabilitiesTests
{
    private readonly Mock<IProcessRunner> _processRunner = new();

    [Fact]
    public async Task ProbeEncoders_ParsesCorrectly()
    {
        string encoderOutput = """
            Encoders:
             V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10 (codec h264)
             V..... libx265              libx265 H.265 / HEVC (codec hevc)
             V..... h264_nvenc           NVIDIA NVENC H.264 encoder (codec h264)
             V..... hevc_nvenc           NVIDIA NVENC hevc encoder (codec hevc)
             V..... libsvtav1            SVT-AV1(Scalable Video Technology for AV1) encoder (codec av1)
             A..... aac                  AAC (Advanced Audio Coding) (codec aac)
             A..... libopus              libopus Opus (codec opus)
            """;
        SetupResponse("-encoders", encoderOutput);
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasEncoder("libx264").Should().BeTrue();
        caps.HasEncoder("h264_nvenc").Should().BeTrue();
        caps.HasEncoder("libsvtav1").Should().BeTrue();
        caps.HasEncoder("aac").Should().BeTrue();
        caps.HasEncoder("vp9_nvenc").Should().BeFalse();
    }

    [Fact]
    public async Task ProbeFilters_ParsesCorrectly()
    {
        SetupResponse("-encoders", "");
        SetupResponse("-protocols", "");
        string filterOutput = """
            Filters:
             ... scale            V->V       Scale the input video size and/or convert the image format.
             ... tonemap          V->V       Conversion of HDR to SDR via tonemapping.
             ... libplacebo       V->V       GPU-accelerated video processing via libplacebo.
             ... zscale           V->V       Scale the input video using z.lib
            """;
        SetupResponse("-filters", filterOutput);

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.HasFilter("libplacebo").Should().BeTrue();
        caps.HasFilter("tonemap").Should().BeTrue();
        caps.HasFilter("zscale").Should().BeTrue();
        caps.HasFilter("nonexistent").Should().BeFalse();
    }

    [Fact]
    public async Task AvailableEncoders_ReturnsImmutableSet()
    {
        string encoderOutput = """
            Encoders:
             V..... libx264              libx264 H.264
            """;
        SetupResponse("-encoders", encoderOutput);
        SetupResponse("-filters", "");
        SetupResponse("-protocols", "");

        FfmpegCapabilities caps = new(_processRunner.Object);
        await caps.ProbeAsync();

        caps.AvailableEncoders.Should().Contain("libx264");
    }

    private void SetupResponse(string flag, string output)
    {
        _processRunner
            .Setup(r =>
                r.RunAsync("ffmpeg", new[] { flag }, (string?)null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ProcessResult(0, output, "", TimeSpan.Zero));
    }
}
