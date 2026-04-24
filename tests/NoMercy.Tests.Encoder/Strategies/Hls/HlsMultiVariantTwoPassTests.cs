namespace NoMercy.Tests.Encoder.Strategies.Hls;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;

/// <summary>
/// Covers the multi-variant 2-pass path added in Tier 1.3 — pass 1 runs once
/// per variant (each with its own stats file), pass 2 runs once with all
/// variants sharing the same run.
/// </summary>
public class HlsMultiVariantTwoPassTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public HlsMultiVariantTwoPassTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"MultiVar_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass1ThreeTimesWithIncreasingIndex()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        List<int> pass1Indices = [];
        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One)
                        pass1Indices.Add(req.Options.Pass1VariantIndex);
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(variantCount: 3), null, CancellationToken.None);

        Assert.Equal(new[] { 0, 1, 2 }, pass1Indices);
    }

    [Fact]
    public async Task Encode_ThreeVariants_RunsPass2ExactlyOnce()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(variantCount: 3), null, CancellationToken.None);

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

    [Fact]
    public async Task Encode_Pass1FailsOnSecondVariant_Pass2NotRun()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One && req.Options.Pass1VariantIndex == 1)
                        return Fail("pass1 variant 1 exploded");
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            BuildRequest(variantCount: 3),
            null,
            CancellationToken.None
        );

        Assert.False(result.Success);
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        // Pass 1 ran for variant 0 then failed on variant 1 → 2 calls, not 3.
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Encode_ResumeWithAllVariantStatsPresent_SkipsPass1()
    {
        // All per-variant stats files must exist for resume to skip pass 1.
        string statsDir = Path.Combine(_outputDir, ".2pass");
        Directory.CreateDirectory(statsDir);
        string statsFile = Path.Combine(statsDir, "x264");
        for (int i = 0; i < 3; i++)
            await File.WriteAllTextAsync($"{statsFile}_v{i}-0.log", $"variant {i} done");

        JobCheckpoint existing = new(
            JobId: "job-1",
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFile,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(variantCount: 3), null, CancellationToken.None);

        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Encode_ResumeWithPartialVariantStats_ReRunsAllPass1()
    {
        // Only variant 0's stats exists — variant 1 + 2 are missing. Pass 1
        // has to re-run for all variants; mixing stale and fresh stats across
        // variants gives unreliable quality.
        string statsDir = Path.Combine(_outputDir, ".2pass");
        Directory.CreateDirectory(statsDir);
        string statsFile = Path.Combine(statsDir, "x264");
        await File.WriteAllTextAsync($"{statsFile}_v0-0.log", "stale");

        JobCheckpoint existing = new(
            JobId: "job-1",
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFile,
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(variantCount: 3), null, CancellationToken.None);

        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
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
            .ReturnsAsync(Success());
    }

    private static EncodingResult Success() =>
        new(
            Success: true,
            OutputPath: "/out",
            Duration: TimeSpan.FromSeconds(1),
            Error: null,
            Metrics: new(1024, 2.0, 24.0, "libx264", null)
        );

    private static EncodingResult Fail(string message) =>
        new(
            Success: false,
            OutputPath: string.Empty,
            Duration: TimeSpan.Zero,
            Error: new(
                NoMercy.Encoder.Errors.EncodingErrorKind.ProcessCrashed,
                message,
                null,
                "Pass1",
                false
            ),
            Metrics: new(0, 0, 0, string.Empty, null)
        );

    private HlsTwoPassStrategy BuildStrategy() =>
        new(
            _encoder.Object,
            _checkpointStore.Object,
            NullLogger<HlsTwoPassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest(int variantCount) =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: $"HLS 2-pass {variantCount}-variant",
                Format: OutputFormat.Hls,
                VideoOutputs: Enumerable
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
                    .ToArray(),
                AudioOutputs: [],
                SubtitleOutputs: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
