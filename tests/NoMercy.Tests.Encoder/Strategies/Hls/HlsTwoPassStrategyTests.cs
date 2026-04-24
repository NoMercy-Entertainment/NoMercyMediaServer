namespace NoMercy.Tests.Encoder.Strategies.Hls;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;

public class HlsTwoPassStrategyTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public HlsTwoPassStrategyTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"HlsTwoPass_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FormatAndMode_AreHlsTwoPass()
    {
        HlsTwoPassStrategy strategy = BuildStrategy();

        Assert.Equal(OutputFormat.Hls, strategy.Format);
        Assert.Equal(EncodeMode.TwoPass, strategy.EncodeMode);
    }

    [Fact]
    public async Task Encode_CallsEncoderTwice_Pass1ThenPass2()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            BuildRequest(),
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

    [Fact]
    public async Task Encode_Pass1AndPass2_ShareSameStatsFilePath()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        string? pass1Stats = null;
        string? pass2Stats = null;
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
                        pass1Stats = req.Options.StatsFilePath;
                    else if (req.Options!.Pass == EncodingPass.Two)
                        pass2Stats = req.Options.StatsFilePath;
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(), null, CancellationToken.None);

        Assert.NotNull(pass1Stats);
        Assert.NotNull(pass2Stats);
        Assert.Equal(pass1Stats, pass2Stats);
    }

    [Fact]
    public async Task Encode_Pass1Failure_Pass2NotRun_AndFailureReturned()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Fail("pass1 exploded"));

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            BuildRequest(),
            null,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Contains("pass1 exploded", result.Error!.Message);
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Encode_Pass1Success_SavesCheckpoint()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(), null, CancellationToken.None);

        _checkpointStore.Verify(
            s =>
                s.SaveAsync(
                    It.Is<JobCheckpoint>(c =>
                        c.Pass1Completed
                        && !string.IsNullOrEmpty(c.StatsFilePath)
                        && c.EncodeMode == "TwoPass"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Encode_FullSuccess_DeletesCheckpoint()
    {
        _checkpointStore
            .Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(), null, CancellationToken.None);

        _checkpointStore.Verify(
            s => s.DeleteAsync(_outputDir, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Encode_ResumeWithValidCheckpoint_SkipsPass1()
    {
        // Per-variant stats files must exist on disk — the resume check walks
        // 0..N-1 and requires each `{base}_v{i}-0.log` to be present.
        string statsDir = Path.Combine(_outputDir, ".2pass");
        Directory.CreateDirectory(statsDir);
        string statsFile = Path.Combine(statsDir, "x264");
        // BuildRequest() uses a profile with zero VideoOutputs → variantCount
        // clamps to 1, so a single _v0-0.log is enough for the resume check.
        await File.WriteAllTextAsync($"{statsFile}_v0-0.log", "pass1 done");

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
        await strategy.EncodeAsync(BuildRequest(), null, CancellationToken.None);

        // Pass 1 should NOT have been called; pass 2 was.
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r =>
                        r.Options!.Pass == EncodingPass.Two && r.Options.StatsFilePath == statsFile
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Encode_ResumeWithCheckpointButStatsFileMissing_ReRunsPass1()
    {
        // Checkpoint claims pass 1 done but the stats file doesn't exist on disk
        // — pretend a previous run was interrupted mid-cleanup. Must re-run.
        JobCheckpoint stale = new(
            JobId: "job-1",
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            CompletedGroupIndices: [],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: Path.Combine(_outputDir, "nonexistent-stats"),
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(BuildRequest(), null, CancellationToken.None);

        _encoder.Verify(
            e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
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
            Error: new(EncodingErrorKind.ProcessCrashed, message, null, "Pass1", false),
            Metrics: new(0, 0, 0, string.Empty, null)
        );

    private HlsTwoPassStrategy BuildStrategy() =>
        new(
            _encoder.Object,
            _checkpointStore.Object,
            NullLogger<HlsTwoPassStrategy>.Instance,
            TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest() =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "HLS 2-pass 1080p",
                Format: OutputFormat.Hls,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
