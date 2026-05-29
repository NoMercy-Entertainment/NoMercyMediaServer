using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Verifies that <see cref="TwoPassStrategyBase"/> dispatches exactly ONE
/// encoder call when <c>Options.Pass</c> is set explicitly by the coordinator.
/// This prevents the S1 regression where Pass2 child tasks re-ran Pass1.
/// </summary>
public class TwoPassSplitTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();
    private readonly HlsTwoPassStrategy _strategy;

    public TwoPassSplitTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"TwoPassSplit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);

        _encoder
            .Setup(encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );

        _checkpointStore
            .Setup(store => store.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        _strategy = new HlsTwoPassStrategy(
            _encoder.Object,
            _checkpointStore.Object,
            NullLogger<HlsTwoPassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task EncodeAsync_PassOneExplicit_CallsEncoderExactlyOnceWithPassOne()
    {
        EncodingRequest request = BuildRequest(EncodingPass.One);

        EncodingResult result = await _strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();

        _encoder.Verify(
            encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.One
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "pass=1 should call the encoder exactly once with Pass == One"
        );

        // Must NOT invoke encoder with Pass == Two when only Pass1 is requested.
        _encoder.Verify(
            encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.Two
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "pass=1 must not invoke a pass=2 encode"
        );
    }

    [Fact]
    public async Task EncodeAsync_PassTwoExplicit_CallsEncoderExactlyOnceWithPassTwo()
    {
        string statsPath = Path.Combine(_outputDir, ".2pass", "x264");
        EncodingRequest request = BuildRequest(EncodingPass.Two, statsPath);

        EncodingResult result = await _strategy.EncodeAsync(
            request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();

        _encoder.Verify(
            encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.Two
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once,
            "pass=2 should call the encoder exactly once with Pass == Two"
        );

        // Must NOT invoke encoder with Pass == One when only Pass2 is requested.
        _encoder.Verify(
            encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.One
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never,
            "pass=2 must not re-run pass=1"
        );
    }

    [Fact]
    public async Task EncodeAsync_NullPass_CallsEncoderTwice()
    {
        // Legacy inline path (no explicit pass) must still run both passes.
        EncodingRequest request = BuildRequest(passOverride: null);

        await _strategy.EncodeAsync(request, progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2),
            "inline two-pass must call the encoder twice (pass1 then pass2)"
        );
    }

    private EncodingRequest BuildRequest(EncodingPass? passOverride, string? statsFilePath = null)
    {
        return new EncodingRequest(
            InputPath: Path.Combine(_outputDir, "input.mp4"),
            OutputDirectory: _outputDir,
            Profile: new NoMercy.Encoder.Profiles.EncodingProfile(
                Id: Ulid.NewUlid(),
                Name: "test",
                Container: NoMercy.Encoder.Profiles.Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            ),
            MediaTitle: "test",
            SourceStorage: TestStorageFactory.CreateLocal(),
            DestinationStorage: TestStorageFactory.CreateLocal(),
            Options: passOverride.HasValue
                ? new NoMercy.Encoder.Pipeline.EncodingOptions
                {
                    Pass = passOverride.Value,
                    StatsFilePath = statsFilePath,
                }
                : null
        );
    }
}
