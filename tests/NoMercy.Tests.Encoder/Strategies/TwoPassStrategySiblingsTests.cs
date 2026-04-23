namespace NoMercy.Tests.Encoder.Strategies;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mp4;

/// <summary>
/// Covers the MP4 / DASH siblings that share <see cref="TwoPassStrategyBase"/>
/// with <c>HlsTwoPassStrategy</c>. The shared orchestration is already covered
/// in depth by <c>HlsTwoPassStrategyTests</c> — here we only verify that each
/// sibling reports the correct format + mode and runs two passes through the
/// injected encoder.
/// </summary>
public class TwoPassStrategySiblingsTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public TwoPassStrategySiblingsTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"TwoPassSiblings_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    public static TheoryData<IEncodingStrategy, OutputFormat> SiblingsWithFormat()
    {
        Mock<IEncoder> encoder = new();
        Mock<ICheckpointStore> store = new();
        return new()
        {
            {
                new HlsTwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<HlsTwoPassStrategy>.Instance
                ),
                OutputFormat.Hls
            },
            {
                new Mp4TwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<Mp4TwoPassStrategy>.Instance
                ),
                OutputFormat.Mp4
            },
            {
                new DashTwoPassStrategy(
                    encoder.Object,
                    store.Object,
                    NullLogger<DashTwoPassStrategy>.Instance
                ),
                OutputFormat.Dash
            },
        };
    }

    [Theory]
    [MemberData(nameof(SiblingsWithFormat))]
    public void Siblings_ReportCorrectFormatAndTwoPassMode(
        IEncodingStrategy strategy,
        OutputFormat expectedFormat
    )
    {
        Assert.Equal(expectedFormat, strategy.Format);
        Assert.Equal(EncodeMode.TwoPass, strategy.EncodeMode);
    }

    [Theory]
    [InlineData(OutputFormat.Mp4)]
    [InlineData(OutputFormat.Dash)]
    public async Task Siblings_CallEncoderTwice_Pass1ThenPass2(OutputFormat format)
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format);
        EncodingResult result = await strategy.EncodeAsync(
            BuildRequest(format),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(result.Success);
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Theory]
    [InlineData(OutputFormat.Mp4)]
    [InlineData(OutputFormat.Dash)]
    public async Task Siblings_SavesCheckpointBetweenPasses(OutputFormat format)
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        IEncodingStrategy strategy = Build(format);
        await strategy.EncodeAsync(BuildRequest(format), null, CancellationToken.None);

        _checkpointStore.Verify(
            s =>
                s.SaveAsync(
                    It.Is<JobCheckpoint>(c => c.Pass1Completed && c.EncodeMode == "TwoPass"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    private void SetupSuccessfulEncoder()
    {
        _encoder
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
    }

    private IEncodingStrategy Build(OutputFormat format) =>
        format switch
        {
            OutputFormat.Mp4 => new Mp4TwoPassStrategy(
                _encoder.Object,
                _checkpointStore.Object,
                NullLogger<Mp4TwoPassStrategy>.Instance
            ),
            OutputFormat.Dash => new DashTwoPassStrategy(
                _encoder.Object,
                _checkpointStore.Object,
                NullLogger<DashTwoPassStrategy>.Instance
            ),
            _ => throw new ArgumentException($"No 2-pass sibling for {format}"),
        };

    private EncodingRequest BuildRequest(OutputFormat format) =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: $"{format} 2-pass",
                Format: format,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
