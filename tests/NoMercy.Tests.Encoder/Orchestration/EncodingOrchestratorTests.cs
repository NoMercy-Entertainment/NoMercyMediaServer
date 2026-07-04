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
            .Setup(s => s.AcquireLocalPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => new LocalPathLease(path));

        // Driver is accessed when naming the publish stage — provide a non-null stub.
        _storage.Setup(s => s.Driver).Returns(new LocalStorageDriver());
    }

    [Fact]
    public async Task EncodeAsync_DispatchesToResolvedStrategy()
    {
        EncodingRequest request = BuildRequest(OutputFormat.Hls, EncodeMode.SinglePass);
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            OutputFormat.Hls,
            EncodeMode.SinglePass,
            success: true
        );

        _resolver
            .Setup(r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(strategy.Object);

        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        EncodingResult result = await orchestrator.EncodeAsync(request);

        Assert.True(result.Success);
        strategy.Verify(
            s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task EncodeAsync_NoStrategyRegistered_ReturnsErrorResult()
    {
        EncodingRequest request = BuildRequest(OutputFormat.Dash, EncodeMode.TwoPass);
        _resolver
            .Setup(r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass))
            .Returns((IEncodingStrategy?)null);

        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        EncodingResult result = await orchestrator.EncodeAsync(request);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("No strategy registered", result.Error!.Message);
        Assert.Contains("Dash", result.Error.Message);
        Assert.Contains("TwoPass", result.Error.Message);
    }

    [Fact]
    public async Task EncodeAsync_NoStrategy_NotifiesProgressObserverOfError()
    {
        EncodingRequest request = BuildRequest(OutputFormat.Mp4, EncodeMode.TwoPass);
        _resolver
            .Setup(r => r.Resolve(It.IsAny<OutputFormat>(), It.IsAny<EncodeMode>()))
            .Returns((IEncodingStrategy?)null);

        Mock<IProgressObserver> progress = new();
        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request, progress.Object);

        progress.Verify(p => p.OnError(It.IsAny<EncodingError>()), Times.Once);
    }

    [Fact]
    public async Task EncodeAsync_ResolvesStrategyBasedOnProfileFormatAndMode()
    {
        // Profile says DASH+TwoPass → resolver must be called with those, not something else.
        EncodingRequest request = BuildRequest(OutputFormat.Dash, EncodeMode.TwoPass);
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            OutputFormat.Dash,
            EncodeMode.TwoPass,
            success: true
        );
        _resolver
            .Setup(r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass))
            .Returns(strategy.Object);

        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        await orchestrator.EncodeAsync(request);

        _resolver.Verify(r => r.Resolve(OutputFormat.Dash, EncodeMode.TwoPass), Times.Once);
        _resolver.Verify(
            r => r.Resolve(It.IsNotIn(OutputFormat.Dash), It.IsAny<EncodeMode>()),
            Times.Never
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
        string escapeTarget = Path.Combine(
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar),
            "..",
            "..",
            "..",
            $"nm-orch-escape-{Guid.NewGuid():N}"
        );
        string resolvedEscapeTarget = Path.GetFullPath(escapeTarget);

        EncodingRequest request = BuildRequest(OutputFormat.Hls, EncodeMode.SinglePass) with
        {
            OutputDirectory = "../../../" + Path.GetFileName(resolvedEscapeTarget),
        };
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            OutputFormat.Hls,
            EncodeMode.SinglePass,
            success: true
        );
        _resolver
            .Setup(r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(strategy.Object);

        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request);

            result.Success.Should().BeFalse();
            result.Status.Should().Be("failed");
            Directory.Exists(resolvedEscapeTarget).Should().BeFalse();
            strategy.Verify(
                s =>
                    s.EncodeAsync(
                        It.IsAny<EncodingRequest>(),
                        It.IsAny<IProgressObserver?>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never,
                "the containment check must reject the temp dir before the strategy ever runs"
            );
        }
        finally
        {
            if (Directory.Exists(resolvedEscapeTarget))
                Directory.Delete(resolvedEscapeTarget, recursive: true);
        }
    }

    [Fact]
    public async Task EncodeAsync_OutputDirectoryIsRootedPath_FailsWithoutTouchingFilesystem()
    {
        // A Windows drive-letter path (or any already-rooted path) survives
        // the '/' trim untouched, and Path.Combine(root, rooted) discards
        // root entirely per .NET's documented behavior.
        string rootedEscapeTarget = Path.Combine(
            Directory.GetParent(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar))!.FullName,
            $"nm-orch-rooted-escape-{Guid.NewGuid():N}"
        );

        EncodingRequest request = BuildRequest(OutputFormat.Hls, EncodeMode.SinglePass) with
        {
            OutputDirectory = rootedEscapeTarget,
        };
        Mock<IEncodingStrategy> strategy = BuildStrategy(
            OutputFormat.Hls,
            EncodeMode.SinglePass,
            success: true
        );
        _resolver
            .Setup(r => r.Resolve(OutputFormat.Hls, EncodeMode.SinglePass))
            .Returns(strategy.Object);

        EncodingOrchestrator orchestrator = new(
            _resolver.Object,
            _storage.Object,
            _encoder.Object,
            NullLogger<EncodingOrchestrator>.Instance
        );

        try
        {
            EncodingResult result = await orchestrator.EncodeAsync(request);

            result.Success.Should().BeFalse();
            result.Status.Should().Be("failed");
            Directory.Exists(rootedEscapeTarget).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(rootedEscapeTarget))
                Directory.Delete(rootedEscapeTarget, recursive: true);
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
                Container: ToContainer(format),
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
        mock.Setup(s => s.Format).Returns(format);
        mock.Setup(s => s.EncodeMode).Returns(mode);
        mock.Setup(s =>
                s.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new EncodingResult(
                    Success: success,
                    OutputPath: "/out",
                    Duration: TimeSpan.Zero,
                    Error: null,
                    Metrics: new(0, 0, 0, "test", null)
                )
            );
        return mock;
    }
}
