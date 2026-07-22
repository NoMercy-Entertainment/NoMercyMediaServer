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
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// SinglePassStrategyBase.DeletePartialOutput swept the ENTIRE output
/// directory on a non-cancel failure or cancellation. For a per-task encode
/// (TaskFilter set, e.g. one audio track of an HLS group) that directory is
/// SHARED with every other decomposed task in the same coordinator group —
/// sweeping it destroys sibling tasks' already-written segments. The fix
/// mirrors EncodingOrchestrator's isPerTaskRun guard: per-task runs skip the
/// sweep entirely and leave cleanup to the coordinator; a bundled Whole task
/// (the only execution for its group) still cleans up like a plain run.
/// </summary>
public class SinglePassStrategyBaseCrashCleanupTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalStorage _storage;

    public SinglePassStrategyBaseCrashCleanupTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-strategy-crash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);
        _storage = TestStorageFactory.CreateLocal();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(path: _tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static EncodingRequest BuildRequest(string outputDir, DecomposedTask? taskFilter) =>
        new(
            InputPath: "/media/test.mkv",
            OutputDirectory: outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "HLS 1080p",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            ),
            Options: taskFilter is null ? null : new EncodingOptions(TaskFilter: taskFilter)
        );

    private static DecomposedTask PerTaskFilter(EncodeTaskKind kind) =>
        new(
            TaskId: "g-task-0",
            ParentJobId: 0,
            GroupTag: "g",
            Kind: kind,
            OutputIndex: 0,
            Resources: null
        );

    private HlsSinglePassStrategy MakeStrategy(EncodingResult result)
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: result);

        return new(encoder: encoder.Object, logger: NullLogger<HlsSinglePassStrategy>.Instance, storage: _storage);
    }

    private HlsSinglePassStrategy MakeStrategyThatThrows(Exception exception)
    {
        Mock<IEncoder> encoder = new();
        encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception: exception);

        return new(encoder: encoder.Object, logger: NullLogger<HlsSinglePassStrategy>.Instance, storage: _storage);
    }

    private static readonly EncodingResult FailedResult = new(
        Success: false,
        OutputPath: string.Empty,
        Duration: TimeSpan.Zero,
        Error: null,
        Metrics: null
    );

    [Fact]
    public async Task EncodeAsync_FailureOnPerTaskRun_LeavesSiblingOutputFileIntact()
    {
        string siblingFile = Path.Combine(path1: _tempDir, path2: "sibling_video.m3u8");
        File.WriteAllText(path: siblingFile, contents: "sibling data");

        HlsSinglePassStrategy strategy = MakeStrategy(result: FailedResult);
        EncodingRequest request = BuildRequest(outputDir: _tempDir, taskFilter: PerTaskFilter(kind: EncodeTaskKind.Audio));

        await strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        File.Exists(path: siblingFile).Should().BeTrue();
    }

    [Fact]
    public async Task EncodeAsync_CancelledOnPerTaskRun_LeavesSiblingOutputFileIntact()
    {
        string siblingFile = Path.Combine(path1: _tempDir, path2: "sibling_video.m3u8");
        File.WriteAllText(path: siblingFile, contents: "sibling data");

        HlsSinglePassStrategy strategy = MakeStrategyThatThrows(exception: new OperationCanceledException());
        EncodingRequest request = BuildRequest(outputDir: _tempDir, taskFilter: PerTaskFilter(kind: EncodeTaskKind.Video));

        Func<Task> act = () =>
            strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(path: siblingFile).Should().BeTrue();
    }

    [Fact]
    public async Task EncodeAsync_FailureOnWholeRun_StillSweepsPartialOutput()
    {
        // No TaskFilter at all — a plain (non-decomposed) strategy run must
        // keep the existing crash-cleanup behavior.
        string partialFile = Path.Combine(path1: _tempDir, path2: "partial.ts");
        File.WriteAllText(path: partialFile, contents: "partial data");

        HlsSinglePassStrategy strategy = MakeStrategy(result: FailedResult);
        EncodingRequest request = BuildRequest(outputDir: _tempDir, taskFilter: null);

        await strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        File.Exists(path: partialFile).Should().BeFalse();
    }

    [Fact]
    public async Task EncodeAsync_FailureOnBundledWholeTaskFilter_StillSweepsPartialOutput()
    {
        // A dispatch-time smart-combine bundle carries Kind == Whole even
        // though TaskFilter is set — it IS the only execution for its group,
        // so it must clean up like a plain Whole run, not defer to a
        // coordinator sweep that will never happen for it.
        string partialFile = Path.Combine(path1: _tempDir, path2: "partial.ts");
        File.WriteAllText(path: partialFile, contents: "partial data");

        HlsSinglePassStrategy strategy = MakeStrategy(result: FailedResult);
        EncodingRequest request = BuildRequest(outputDir: _tempDir, taskFilter: PerTaskFilter(kind: EncodeTaskKind.Whole));

        await strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        File.Exists(path: partialFile).Should().BeFalse();
    }

    [Fact]
    public async Task EncodeAsync_SuccessOnPerTaskRun_DoesNotTouchOutputDirectory()
    {
        string siblingFile = Path.Combine(path1: _tempDir, path2: "sibling_video.m3u8");
        File.WriteAllText(path: siblingFile, contents: "sibling data");

        EncodingResult successResult = new(
            Success: true,
            OutputPath: _tempDir,
            Duration: TimeSpan.FromSeconds(seconds: 1),
            Error: null,
            Metrics: new(OutputSizeBytes: 1024, AverageSpeed: 2.0, AverageFps: 24.0, EncoderUsed: "aac", GpuUsed: null)
        );
        HlsSinglePassStrategy strategy = MakeStrategy(result: successResult);
        EncodingRequest request = BuildRequest(outputDir: _tempDir, taskFilter: PerTaskFilter(kind: EncodeTaskKind.Audio));

        EncodingResult result = await strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeTrue();
        File.Exists(path: siblingFile).Should().BeTrue();
    }
}
