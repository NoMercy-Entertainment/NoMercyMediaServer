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
        _outputDir = Path.Combine(path1: Path.GetTempPath(), path2: $"TwoPassSplit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _outputDir);

        _encoder
            .Setup(expression: encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "test", GpuUsed: null)
                )
            );

        _checkpointStore
            .Setup(expression: store => store.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        _strategy = new(
            encoder: _encoder.Object,
            checkpointStore: _checkpointStore.Object,
            logger: NullLogger<HlsTwoPassStrategy>.Instance,
            storage: TestStorageFactory.CreateLocal()
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _outputDir))
            Directory.Delete(path: _outputDir, recursive: true);
        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task EncodeAsync_PassOneExplicit_CallsEncoderExactlyOnceWithPassOne()
    {
        EncodingRequest request = BuildRequest(passOverride: EncodingPass.One);

        EncodingResult result = await _strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();

        _encoder.Verify(
            expression: encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.One
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "pass=1 should call the encoder exactly once with Pass == One"
        );

        // Must NOT invoke encoder with Pass == Two when only Pass1 is requested.
        _encoder.Verify(
            expression: encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.Two
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "pass=1 must not invoke a pass=2 encode"
        );
    }

    [Fact]
    public async Task EncodeAsync_PassTwoExplicit_CallsEncoderExactlyOnceWithPassTwo()
    {
        string statsPath = Path.Combine(path1: _outputDir, path2: ".2pass", path3: "x264");
        EncodingRequest request = BuildRequest(passOverride: EncodingPass.Two, statsFilePath: statsPath);

        EncodingResult result = await _strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();

        _encoder.Verify(
            expression: encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.Two
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once,
            failMessage: "pass=2 should call the encoder exactly once with Pass == Two"
        );

        // Must NOT invoke encoder with Pass == One when only Pass2 is requested.
        _encoder.Verify(
            expression: encoder =>
                encoder.EncodeAsync(
                    It.Is<EncodingRequest>(encodingRequest =>
                        encodingRequest.Options != null
                        && encodingRequest.Options.Pass == EncodingPass.One
                    ),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Never,
            failMessage: "pass=2 must not re-run pass=1"
        );
    }

    [Fact]
    public async Task EncodeAsync_NullPass_CallsEncoderTwice()
    {
        // Legacy inline path (no explicit pass) must still run both passes.
        EncodingRequest request = BuildRequest(passOverride: null);

        await _strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        _encoder.Verify(
            expression: encoder =>
                encoder.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Exactly(callCount: 2),
            failMessage: "inline two-pass must call the encoder twice (pass1 then pass2)"
        );
    }

    private EncodingRequest BuildRequest(EncodingPass? passOverride, string? statsFilePath = null)
    {
        return new(
            InputPath: Path.Combine(path1: _outputDir, path2: "input.mp4"),
            OutputDirectory: _outputDir,
            Profile: new(
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
                ? new EncodingOptions
                {
                    Pass = passOverride.Value,
                    StatsFilePath = statsFilePath,
                }
                : null
        );
    }
}
