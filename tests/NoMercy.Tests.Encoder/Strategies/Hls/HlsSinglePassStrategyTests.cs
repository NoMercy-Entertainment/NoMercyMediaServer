using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using Container = NoMercy.Encoder.Profiles.V2.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.V2.EncodingProfile;

namespace NoMercy.Tests.Encoder.Strategies.Hls;

public class HlsSinglePassStrategyTests
{
    [Fact]
    public void Format_IsHls()
    {
        HlsSinglePassStrategy strategy = new(Mock.Of<IEncoder>());

        Assert.Equal(OutputFormat.Hls, strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        HlsSinglePassStrategy strategy = new(Mock.Of<IEncoder>());

        Assert.Equal(EncodeMode.SinglePass, strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_DelegatesToInjectedEncoder()
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.FromSeconds(1),
                    Error: null,
                    Metrics: new(1024, 2.0, 24.0, "libx264", null)
                )
            );

        HlsSinglePassStrategy strategy = new(encoder.Object);

        EncodingRequest request = new(
            InputPath: "/media/test.mkv",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "HLS 1080p",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal("libx264", result.Metrics.EncoderUsed);
        encoder.Verify(
            e =>
                e.EncodeAsync(
                    request,
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
