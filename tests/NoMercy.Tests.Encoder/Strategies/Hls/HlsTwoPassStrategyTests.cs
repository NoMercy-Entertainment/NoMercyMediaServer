// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies.Hls;

public class HlsTwoPassStrategyTests : IDisposable
{
    private readonly string _outputDir;
    private readonly Mock<IEncoder> _encoder = new();
    private readonly Mock<ICheckpointStore> _checkpointStore = new();

    public HlsTwoPassStrategyTests()
    {
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"HlsTwoPass_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public void FormatAndMode_AreHlsTwoPass()
    {
        HlsTwoPassStrategy strategy = BuildStrategy();

        Assert.Equal(expected: OutputFormat.Hls, actual: strategy.Format);
        Assert.Equal(expected: EncodeMode.TwoPass, actual: strategy.EncodeMode);
    }

    [Fact]
    public async Task Encode_CallsEncoderTwice_Pass1ThenPass2()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            request: BuildRequest(),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.True(condition: result.Success);
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task Encode_Pass1AndPass2_ShareSameStatsFilePath()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        string? pass1Stats = null;
        string? pass2Stats = null;
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                valueFunction: (EncodingRequest req, IProgressObserver? _, CancellationToken _) =>
                {
                    if (req.Options!.Pass == EncodingPass.One)
                        pass1Stats = req.Options.StatsFilePath;
                    else if (req.Options!.Pass == EncodingPass.Two)
                        pass2Stats = req.Options.StatsFilePath;
                    return Success();
                }
            );

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(), progress: null, ct: CancellationToken.None);

        Assert.NotNull(@object: pass1Stats);
        Assert.NotNull(@object: pass2Stats);
        Assert.Equal(expected: pass1Stats, actual: pass2Stats);
    }

    [Fact]
    public async Task Encode_Pass1Failure_Pass2NotRun_AndFailureReturned()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Fail(message: "pass1 exploded"));

        HlsTwoPassStrategy strategy = BuildStrategy();
        EncodingResult result = await strategy.EncodeAsync(
            request: BuildRequest(),
            progress: null,
            ct: CancellationToken.None
        );

        Assert.False(condition: result.Success);
        Assert.Contains(expectedSubstring: "pass1 exploded", actualString: result.Error!.Message);
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.Two),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
    }

    [Fact]
    public async Task Encode_Pass1Success_SavesCheckpoint()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(), progress: null, ct: CancellationToken.None);

        _checkpointStore.Verify(
            expression: s =>
                s.SaveAsync(
                    It.Is<JobCheckpoint>(c =>
                        c.Pass1Completed
                        && !string.IsNullOrEmpty(c.StatsFilePath)
                        && c.EncodeMode == "TwoPass"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task Encode_FullSuccess_DeletesCheckpoint()
    {
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(), progress: null, ct: CancellationToken.None);

        _checkpointStore.Verify(
            expression: s => s.DeleteAsync(_outputDir, It.IsAny<CancellationToken>()),
            times: Times.Once
        );
    }

    [Fact]
    public async Task Encode_ResumeWithValidCheckpoint_SkipsPass1()
    {
        // Per-variant stats files must exist on disk — the resume check walks
        // 0..N-1 and requires each `{base}_v{i}-0.log` to be present.
        string statsDir = Path.Combine(path1: _outputDir, path2: ".2pass");
        Directory.CreateDirectory(path: statsDir);
        string statsFile = Path.Combine(path1: statsDir, path2: "x264");
        // BuildRequest() uses a profile with zero VideoOutputs → variantCount
        // clamps to 1, so a single _v0-0.log is enough for the resume check.
        await File.WriteAllTextAsync(path: $"{statsFile}_v0-0.log", contents: "pass1 done");

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
            .Setup(expression: s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: existing);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(), progress: null, ct: CancellationToken.None);

        // Pass 1 should NOT have been called; pass 2 was.
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never
        );
        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r =>
                        r.Options!.Pass == EncodingPass.Two && r.Options.StatsFilePath == statsFile
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
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
            StatsFilePath: Path.Combine(path1: _outputDir, path2: "nonexistent-stats"),
            Pass1Completed: true,
            LastCompletedSegment: -1,
            EncodeMode: "TwoPass"
        );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(_outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: stale);
        SetupSuccessfulEncoder();

        HlsTwoPassStrategy strategy = BuildStrategy();
        await strategy.EncodeAsync(request: BuildRequest(), progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            expression: e =>
                e.EncodeAsync(
                    It.Is<EncodingRequest>(r => r.Options!.Pass == EncodingPass.One),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    private void SetupSuccessfulEncoder()
    {
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: Success());
    }

    private static EncodingResult Success() =>
        new(
            Success: true,
            OutputPath: "/out",
            Duration: TimeSpan.FromSeconds(seconds: 1),
            Error: null,
            Metrics: new(OutputSizeBytes: 1024, AverageSpeed: 2.0, AverageFps: 24.0, EncoderUsed: "libx264", GpuUsed: null)
        );

    private static EncodingResult Fail(string message) =>
        new(
            Success: false,
            OutputPath: string.Empty,
            Duration: TimeSpan.Zero,
            Error: new(Kind: EncodingErrorKind.ProcessCrashed, Message: message, FfmpegStderr: null, StageName: "Pass1", Recoverable: false),
            Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: string.Empty, GpuUsed: null)
        );

    private HlsTwoPassStrategy BuildStrategy() =>
        new(
            encoder: _encoder.Object,
            checkpointStore: _checkpointStore.Object,
            logger: NullLogger<HlsTwoPassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );

    private EncodingRequest BuildRequest() =>
        new(
            InputPath: "/media/src.mkv",
            OutputDirectory: _outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "HLS 2-pass 1080p",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: EncodeMode.TwoPass
            )
        );
}
