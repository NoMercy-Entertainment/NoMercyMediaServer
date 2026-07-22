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
using NoMercy.Encoder.Orchestration;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Orchestration;

public class EncodingOrchestratorTests
{
    private readonly Mock<IStrategyResolver> _resolver = new();
    private readonly Mock<IStorage> _storage = new();
    private readonly Mock<IEncoder> _encoder = new();

    public EncodingOrchestratorTests()
    {
        // Pass-through lease so tests that reach the staging step don't null-ref.
        _storage
            .Setup(expression: s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(valueFunction: (string path, CancellationToken _) => new(path: path));

        // Driver is accessed when naming the publish stage — provide a non-null stub.
        _storage.Setup(expression: s => s.Driver).Returns(value: new LocalStorageDriver());
    }

    [Fact]
    public async Task EncodeAsync_DispatchesToResolvedStrategy()
    {
        EncodingRequest request = BuildRequest(format: OutputFormat.Hls, mode: EncodeMode.SinglePass);
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            format: OutputFormat.Hls,
            mode: EncodeMode.SinglePass,
            success: true
        );

        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        EncodingResult result = await orchestrator.EncodeAsync(request: request);

        Assert.True(condition: result.Success);
        strategy.Verify(
            expression: s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task EncodeAsync_NoStrategyRegistered_ReturnsErrorResult()
    {
        EncodingRequest request = BuildRequest(format: OutputFormat.Dash, mode: EncodeMode.TwoPass);
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass))
            .Returns(value: (IEncodingStrategy?)null);

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        EncodingResult result = await orchestrator.EncodeAsync(request: request);

        Assert.False(condition: result.Success);
        Assert.NotNull(@object: result.Error);
        Assert.Contains(expectedSubstring: "No strategy registered", actualString: result.Error!.Message);
        Assert.Contains(expectedSubstring: "Dash", actualString: result.Error.Message);
        Assert.Contains(expectedSubstring: "TwoPass", actualString: result.Error.Message);
    }

    [Fact]
    public async Task EncodeAsync_NoStrategy_NotifiesProgressObserverOfError()
    {
        EncodingRequest request = BuildRequest(format: OutputFormat.Mp4, mode: EncodeMode.TwoPass);
        _resolver
            .Setup(expression: r => r.Resolve(It.IsAny<OutputFormat>(), It.IsAny<EncodeMode>()))
            .Returns(value: (IEncodingStrategy?)null);

        Mock<IProgressObserver> progress = new();
        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request: request, progress: progress.Object);

        progress.Verify(expression: p => p.OnError(It.IsAny<EncodingError>()), times: Times.Once);
    }

    [Fact]
    public async Task EncodeAsync_ResolvesStrategyBasedOnProfileFormatAndMode()
    {
        // Profile says DASH+TwoPass → resolver must be called with those, not something else.
        EncodingRequest request = BuildRequest(format: OutputFormat.Dash, mode: EncodeMode.TwoPass);
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            format: OutputFormat.Dash,
            mode: EncodeMode.TwoPass,
            success: true
        );
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request: request);

        _resolver.Verify(expression: r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass), times: Times.Once);
        _resolver.Verify(
            expression: r => r.Resolve(It.IsNotIn(OutputFormat.Dash), It.IsAny<EncodeMode>()),
            times: Times.Never
        );
    }

    // ── Temp-dir containment (Path.Combine + rooted-path / ".." escape) ──────
    //
    // request.OutputDirectory is documented as storage-relative but nothing
    // structurally enforced that: Path.Combine silently discards the root
    // when the second argument is itself rooted, and ".." segments resolve
    // right past it. Both used to reach Directory.CreateDirectory and the
    // recursive Directory.Delete unchecked.

    [Fact]
    public async Task EncodeAsync_OutputDirectoryTraversesAboveTranscodeRoot_FailsWithoutTouchingFilesystem()
    {
        string escapeTarget = Path.Combine(paths: [Path.GetTempPath().TrimEnd(trimChar: Path.DirectorySeparatorChar), "..", "..", "..", $"nm-orch-escape-{Guid.NewGuid():N}"]
        );
        string resolvedEscapeTarget = Path.GetFullPath(path: escapeTarget);

        EncodingRequest request = BuildRequest(format: OutputFormat.Hls, mode: EncodeMode.SinglePass) with
        {
            OutputDirectory = "../../../" + Path.GetFileName(path: resolvedEscapeTarget),
        };
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            format: OutputFormat.Hls,
            mode: EncodeMode.SinglePass,
            success: true
        );
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request: request);

            result.Success.Should().BeFalse();
            result.Status.Should().Be(expected: "failed");
            Directory.Exists(path: resolvedEscapeTarget).Should().BeFalse();
            strategy.Verify(
                expression: s =>
                    s.EncodeAsync(
                        It.IsAny<EncodingRequest>(),
                        It.IsAny<IProgressObserver?>(),
                        It.IsAny<CancellationToken>()
                    ),
                times: Times.Never,
                failMessage: "the containment check must reject the temp dir before the strategy ever runs"
            );
        }
        finally
        {
            if (Directory.Exists(path: resolvedEscapeTarget))
                Directory.Delete(path: resolvedEscapeTarget, recursive: true);
        }
    }

    [Fact]
    public async Task EncodeAsync_OutputDirectoryIsRootedPath_FailsWithoutTouchingFilesystem()
    {
        // A Windows drive-letter path (or any already-rooted path) survives
        // the '/' trim untouched, and Path.Combine(root, rooted) discards
        // root entirely per .NET's documented behavior.
        string rootedEscapeTarget = Path.Combine(
            path1: Directory.GetParent(path: Path.GetTempPath().TrimEnd(trimChar: Path.DirectorySeparatorChar))!.FullName,
            path2: $"nm-orch-rooted-escape-{Guid.NewGuid():N}"
        );

        EncodingRequest request = BuildRequest(format: OutputFormat.Hls, mode: EncodeMode.SinglePass) with
        {
            OutputDirectory = rootedEscapeTarget,
        };
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            format: OutputFormat.Hls,
            mode: EncodeMode.SinglePass,
            success: true
        );
        _resolver
            .Setup(expression: r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(value: strategy.Object);

        EncodingOrchestrator orchestrator = new(
            resolver: _resolver.Object,
            storage: _storage.Object,
            encoder: _encoder.Object,
            logger: NullLogger<EncodingOrchestrator>.Instance
        );

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request: request);

            result.Success.Should().BeFalse();
            result.Status.Should().Be(expected: "failed");
            Directory.Exists(path: rootedEscapeTarget).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(path: rootedEscapeTarget))
                Directory.Delete(path: rootedEscapeTarget, recursive: true);
        }
    }

    private static Container ToContainer(OutputFormat format) =>
        format switch
        {
            OutputFormat.Hls => Container.HlsTs,
            OutputFormat.Dash => Container.Dash,
            OutputFormat.Mp4 => Container.Mp4,
            OutputFormat.Mkv => Container.Mkv,
            _ => Container.HlsTs,
        };

    private static EncodingRequest BuildRequest(OutputFormat format, EncodeMode mode) =>
        new(
            InputPath: "/media/test.mkv",
            // Storage-relative, no leading separator — OutputDirectory is
            // documented as relative-to-TranscodeRoot and real callers
            // (VideoEncodeJob) never emit a leading slash here. A leading
            // '/' is native-OS-rooted on Linux too (not just Windows-style
            // absolutes), so it would now trip the cross-platform rootedness
            // guard the same way a genuine escape attempt does.
            OutputDirectory: "out",
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test",
                Container: ToContainer(format: format),
                Video: null,
                Audio: [],
                Subtitles: [],
                EncodeMode: mode
            )
        );

    private static Mock<IEncodingStrategy> BuildStrategy(
        OutputFormat format,
        EncodeMode mode,
        bool success
    )
    {
        Mock<IEncodingStrategy> mock = new();
        mock.Setup(expression: s => s.Format).Returns(value: format);
        mock.Setup(expression: s => s.EncodeMode).Returns(value: mode);
        mock.Setup(expression: s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: success,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(OutputSizeBytes: 0, AverageSpeed: 0, AverageFps: 0, EncoderUsed: "test", GpuUsed: null)
                )
            );
        return mock;
    }
}
