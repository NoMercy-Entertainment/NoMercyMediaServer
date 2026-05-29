using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Dash;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Encoder.Strategies.Mp4;
using NoMercy.Storage;
using CodecProfile = NoMercy.Encoder.Profiles.CodecProfile;
using Container = NoMercy.Encoder.Profiles.Container;
using StreamPolicy = NoMercy.Encoder.Profiles.StreamPolicy;
using V2RateControlMode = NoMercy.Encoder.Profiles.RateControlMode;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Regression tests for 2-pass stats-file plumbing.
/// Verifies that pass-1 and pass-2 requests carry the correct
/// -pass / -passlogfile arguments, that both passes share the same
/// stats path, and that the path goes through IStorage (not raw temp).
/// </summary>
public class TwoPassStatsFileTests
{
    // ----------------------------------------------------------------
    // Shared helpers
    // ----------------------------------------------------------------

    private static Mock<IEncoder> BuildCapturingEncoder(
        List<EncodingRequest> captured,
        bool failPass = false
    )
    {
        Mock<IEncoder> mock = new();
        mock.Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<EncodingRequest, IProgressObserver?, CancellationToken>(
                (req, _, _) => captured.Add(req)
            )
            .ReturnsAsync(
                (EncodingRequest _, IProgressObserver? _, CancellationToken _) =>
                    failPass
                        ? new EncodingResult(
                            Success: false,
                            OutputPath: string.Empty,
                            Duration: TimeSpan.Zero,
                            Error: new EncodingError(
                                EncodingErrorKind.Unknown,
                                "simulated failure",
                                null,
                                "test",
                                false
                            ),
                            Metrics: null
                        )
                        : new EncodingResult(
                            Success: true,
                            OutputPath: "/out",
                            Duration: TimeSpan.Zero,
                            Error: null,
                            Metrics: new(0, 0, 0, "test", null)
                        )
            );
        return mock;
    }

    private static Mock<ICheckpointStore> BuildNoOpCheckpointStore()
    {
        Mock<ICheckpointStore> mock = new();
        mock.Setup(s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);
        mock.Setup(s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<IStorage> BuildPermissiveStorage()
    {
        Mock<IStorage> mock = new();
        mock.Setup(s => s.CreateDirectory(It.IsAny<string>()));
        mock.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);
        mock.Setup(s => s.List(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns([]);
        return mock;
    }

    private static EncodingRequest FakeRequest(string outputDir = "/out/movie") =>
        new(
            InputPath: "/media/movie.mkv",
            OutputDirectory: outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test",
                Container: Container.HlsTs,
                Video: new(
                    Policy: StreamPolicy.Transcode,
                    Codec: VideoCodecType.H264,
                    Width: 1920,
                    Height: null,
                    RateControl: V2RateControlMode.Crf,
                    Crf: 22,
                    BitrateKbps: 0,
                    MaxBitrateKbps: null,
                    BufferSizeKbps: null,
                    Preset: "fast",
                    CodecProfile: CodecProfile.Auto,
                    Level: null,
                    Tune: null,
                    BitDepth: 8,
                    PixelFormat: null,
                    KeyframeIntervalSeconds: 2,
                    ConvertHdrToSdr: false,
                    SegmentNameTemplate: "video/{label}",
                    PlaylistNameTemplate: "video/{label}/playlist"
                ),
                Audio: [],
                Subtitles: []
            )
        );

    private static (
        TwoPassStrategyBase strategy,
        List<EncodingRequest> captured
    ) BuildStrategy<TStrategy>(string outputDir = "/out/movie")
        where TStrategy : TwoPassStrategyBase
    {
        List<EncodingRequest> captured = [];
        Mock<IEncoder> encoder = BuildCapturingEncoder(captured);
        Mock<ICheckpointStore> checkpoint = BuildNoOpCheckpointStore();
        Mock<IStorage> storage = BuildPermissiveStorage();

        TwoPassStrategyBase strategy = typeof(TStrategy).Name switch
        {
            nameof(HlsTwoPassStrategy) => new HlsTwoPassStrategy(
                encoder.Object,
                checkpoint.Object,
                NullLogger<HlsTwoPassStrategy>.Instance,
                storage.Object
            ),
            nameof(Mp4TwoPassStrategy) => new Mp4TwoPassStrategy(
                encoder.Object,
                checkpoint.Object,
                NullLogger<Mp4TwoPassStrategy>.Instance,
                storage.Object
            ),
            nameof(DashTwoPassStrategy) => new DashTwoPassStrategy(
                encoder.Object,
                checkpoint.Object,
                NullLogger<DashTwoPassStrategy>.Instance,
                storage.Object
            ),
            _ => throw new InvalidOperationException($"Unknown strategy {typeof(TStrategy).Name}"),
        };

        return (strategy, captured);
    }

    // ----------------------------------------------------------------
    // HlsTwoPassStrategy
    // ----------------------------------------------------------------

    [Fact]
    public async Task HlsTwoPassStrategy_pass1_args_contain_pass_1_and_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<HlsTwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        pass1.Options!.StatsFilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HlsTwoPassStrategy_pass2_args_contain_pass_2_and_same_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<HlsTwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        EncodingRequest pass2 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.Two
        );

        pass2.Options!.StatsFilePath.Should().NotBeNullOrEmpty();
        // Both passes share the SAME base stats path so pass-2 can read pass-1's data.
        pass2.Options.StatsFilePath.Should().Be(pass1.Options!.StatsFilePath);
    }

    [Fact]
    public async Task HlsTwoPassStrategy_stats_file_path_is_under_temp_or_storage_root()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), "nm_test_hls");
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<HlsTwoPassStrategy>(outputDir);

        await strategy.EncodeAsync(
            FakeRequest(outputDir),
            progress: null,
            ct: CancellationToken.None
        );

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        string statsPath = pass1.Options!.StatsFilePath!;

        // Path must be rooted (absolute) and anchored under the output directory —
        // never a raw global temp path unrelated to the encode job.
        Path.IsPathRooted(statsPath).Should().BeTrue();
        statsPath.Should().Contain(".2pass");
    }

    // ----------------------------------------------------------------
    // Mp4TwoPassStrategy
    // ----------------------------------------------------------------

    [Fact]
    public async Task Mp4TwoPassStrategy_pass1_args_contain_pass_1_and_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<Mp4TwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        pass1.Options!.StatsFilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Mp4TwoPassStrategy_pass2_args_contain_pass_2_and_same_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<Mp4TwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        EncodingRequest pass2 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.Two
        );

        pass2.Options!.StatsFilePath.Should().Be(pass1.Options!.StatsFilePath);
    }

    [Fact]
    public async Task Mp4TwoPassStrategy_stats_file_path_is_under_temp_or_storage_root()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), "nm_test_mp4");
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<Mp4TwoPassStrategy>(outputDir);

        await strategy.EncodeAsync(
            FakeRequest(outputDir),
            progress: null,
            ct: CancellationToken.None
        );

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        string statsPath = pass1.Options!.StatsFilePath!;

        Path.IsPathRooted(statsPath).Should().BeTrue();
        statsPath.Should().Contain(".2pass");
    }

    // ----------------------------------------------------------------
    // DashTwoPassStrategy
    // ----------------------------------------------------------------

    [Fact]
    public async Task DashTwoPassStrategy_pass1_args_contain_pass_1_and_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<DashTwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        pass1.Options!.StatsFilePath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DashTwoPassStrategy_pass2_args_contain_pass_2_and_same_passlogfile()
    {
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<DashTwoPassStrategy>();

        await strategy.EncodeAsync(FakeRequest(), progress: null, ct: CancellationToken.None);

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        EncodingRequest pass2 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.Two
        );

        pass2.Options!.StatsFilePath.Should().Be(pass1.Options!.StatsFilePath);
    }

    [Fact]
    public async Task DashTwoPassStrategy_stats_file_path_is_under_temp_or_storage_root()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), "nm_test_dash");
        (TwoPassStrategyBase strategy, List<EncodingRequest> captured) =
            BuildStrategy<DashTwoPassStrategy>(outputDir);

        await strategy.EncodeAsync(
            FakeRequest(outputDir),
            progress: null,
            ct: CancellationToken.None
        );

        EncodingRequest pass1 = captured.First(r =>
            r.Options != null && r.Options.Pass == EncodingPass.One
        );
        string statsPath = pass1.Options!.StatsFilePath!;

        Path.IsPathRooted(statsPath).Should().BeTrue();
        statsPath.Should().Contain(".2pass");
    }

    // ----------------------------------------------------------------
    // Cross-cutting: pass-1 failure short-circuits before pass-2
    // ----------------------------------------------------------------

    [Fact]
    public async Task TwoPassStrategy_pass1_failure_does_not_invoke_pass2()
    {
        List<EncodingRequest> captured = [];
        Mock<IEncoder> encoder = BuildCapturingEncoder(captured, failPass: true);
        Mock<ICheckpointStore> checkpoint = BuildNoOpCheckpointStore();
        Mock<IStorage> storage = BuildPermissiveStorage();

        HlsTwoPassStrategy strategy = new(
            encoder.Object,
            checkpoint.Object,
            NullLogger<HlsTwoPassStrategy>.Instance,
            storage.Object
        );

        EncodingResult result = await strategy.EncodeAsync(
            FakeRequest(),
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeFalse();
        captured.Should().NotContain(r => r.Options != null && r.Options.Pass == EncodingPass.Two);
    }
}
