using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Audio;

namespace NoMercy.Tests.Encoder.Strategies.Audio;

public class AudioHlsStrategyTests
{
    [Fact]
    public void Format_IsAudioHls()
    {
        AudioHlsStrategy strategy = new(Mock.Of<IEncoder>());

        Assert.Equal(OutputFormat.AudioHls, strategy.Format);
    }

    [Fact]
    public void EncodeMode_IsSinglePass()
    {
        AudioHlsStrategy strategy = new(Mock.Of<IEncoder>());

        Assert.Equal(EncodeMode.SinglePass, strategy.EncodeMode);
    }

    [Fact]
    public async Task EncodeAsync_AacAudioInput_ProducesPlaylistAndSegments()
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
                    OutputPath: "/out/audio.m3u8",
                    Duration: TimeSpan.FromSeconds(3),
                    Error: null,
                    Metrics: new(128, 1.0, 0.0, "aac", null)
                )
            );

        AudioHlsStrategy strategy = new(encoder.Object);

        EncodingRequest request = new(
            InputPath: "/media/track.aac",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Audio HLS",
                Format: OutputFormat.AudioHls,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal("/out/audio.m3u8", result.OutputPath);
        Assert.Equal("aac", result.Metrics.EncoderUsed);
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

    [Fact]
    public async Task EncodeAsync_FlacInput_TranscodesToAac()
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
                    OutputPath: "/out/audio.m3u8",
                    Duration: TimeSpan.FromSeconds(5),
                    Error: null,
                    Metrics: new(128, 1.0, 0.0, "aac", null)
                )
            );

        AudioHlsStrategy strategy = new(encoder.Object);

        EncodingRequest request = new(
            InputPath: "/media/track.flac",
            OutputDirectory: "/out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Audio HLS from FLAC",
                Format: OutputFormat.AudioHls,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: []
            )
        );

        EncodingResult result = await strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        // The strategy delegates format detection to the planner/output strategy;
        // the pipeline reports the AAC codec that was used.
        Assert.Equal("aac", result.Metrics.EncoderUsed);
    }
}
