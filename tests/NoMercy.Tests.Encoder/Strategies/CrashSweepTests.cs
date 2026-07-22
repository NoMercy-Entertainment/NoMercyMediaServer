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
using NoMercy.Encoder.Errors;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Encoder.Storage;
using Container = NoMercy.Encoder.Profiles.Container;
using EncodingProfile = NoMercy.Encoder.Profiles.EncodingProfile;

namespace NoMercy.Tests.Encoder.Strategies;

/// <summary>
/// Verifies destination-side partial output sweep on encode crash (item 3).
///
/// On a non-cancel failure (ffmpeg exits non-zero), the strategy must delete
/// any partial output the current run wrote to the output directory so the
/// directory is not left in a half-written state. The crash checkpoint written
/// by ExecuteStage is intentionally left intact for the resume path.
///
/// A successful encode must NOT touch the output directory.
/// </summary>
public class CrashSweepTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<IEncoder> _encoder;
    private readonly Mock<ICheckpointStore> _checkpointStore;
    private readonly LocalStorage _storage;
    private readonly HlsTwoPassStrategy _strategy;

    public CrashSweepTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"CrashSweep_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempRoot);

        _encoder = new(behavior: MockBehavior.Strict);
        _checkpointStore = new();
        _storage = TestStorageFactory.CreateLocal();

        _strategy = new(
            encoder: _encoder.Object,
            checkpointStore: _checkpointStore.Object,
            logger: NullLogger<HlsTwoPassStrategy>.Instance,
            storage: _storage
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempRoot))
            Directory.Delete(path: _tempRoot, recursive: true);

        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Crash_DeletesPartialOutputFiles()
    {
        string outputDir = Path.Combine(path1: _tempRoot, path2: "output");
        Directory.CreateDirectory(path: outputDir);

        // Simulate a partial write: one segment file exists before the crash.
        string partialSegment = Path.Combine(path1: outputDir, path2: "video_0.ts");
        await File.WriteAllTextAsync(path: partialSegment, contents: "partial segment data");

        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: false,
                    OutputPath: string.Empty,
                    Duration: TimeSpan.Zero,
                    Error: new(
                        Kind: EncodingErrorKind.ProcessCrashed,
                        Message: "ffmpeg crashed",
                        FfmpegStderr: null,
                        StageName: "Execute",
                        Recoverable: true
                    ),
                    Metrics: null
                )
            );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        EncodingRequest request = new(
            InputPath: "/media/source.mkv",
            OutputDirectory: outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test HLS",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        EncodingResult result = await _strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeFalse();
        File.Exists(path: partialSegment).Should().BeFalse(because: "crash sweep must delete partial segments");
    }

    [Fact]
    public async Task Crash_CheckpointFilePreserved()
    {
        // Crash checkpoint written by ExecuteStage must NOT be deleted by the
        // strategy's partial-sweep — orphan recovery needs it for resume.
        string outputDir = Path.Combine(path1: _tempRoot, path2: "output");
        Directory.CreateDirectory(path: outputDir);

        string checkpointFile = Path.Combine(path1: outputDir, path2: ".checkpoint.json");
        await File.WriteAllTextAsync(path: checkpointFile, contents: "{\"JobId\":\"abc\"}");

        string partialSegment = Path.Combine(path1: outputDir, path2: "video_0.ts");
        await File.WriteAllTextAsync(path: partialSegment, contents: "partial");

        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: false,
                    OutputPath: string.Empty,
                    Duration: TimeSpan.Zero,
                    Error: new(
                        Kind: EncodingErrorKind.ProcessCrashed,
                        Message: "crashed",
                        FfmpegStderr: null,
                        StageName: "Execute",
                        Recoverable: true
                    ),
                    Metrics: null
                )
            );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        EncodingRequest request = new(
            InputPath: "/media/source.mkv",
            OutputDirectory: outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test HLS",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        await _strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        // Checkpoint survives the sweep — the resume path needs it.
        File.Exists(path: checkpointFile).Should().BeTrue(because: "crash checkpoint must survive the sweep");
        // The partial segment is swept.
        File.Exists(path: partialSegment).Should().BeFalse();
    }

    [Fact]
    public async Task Success_DoesNotSweepOutputFiles()
    {
        string outputDir = Path.Combine(path1: _tempRoot, path2: "output");
        Directory.CreateDirectory(path: outputDir);

        string finalSegment = Path.Combine(path1: outputDir, path2: "video_0.ts");
        await File.WriteAllTextAsync(path: finalSegment, contents: "real segment data");

        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                value: new EncodingResult(
                    Success: true,
                    OutputPath: outputDir,
                    Duration: TimeSpan.FromSeconds(seconds: 10),
                    Error: null,
                    Metrics: null
                )
            );

        _checkpointStore
            .Setup(expression: s => s.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        EncodingRequest request = new(
            InputPath: "/media/source.mkv",
            OutputDirectory: outputDir,
            Profile: new(
                Id: Ulid.NewUlid(),
                Name: "Test HLS",
                Container: Container.HlsTs,
                Video: null,
                Audio: [],
                Subtitles: []
            )
        );

        await _strategy.EncodeAsync(request: request, progress: null, ct: CancellationToken.None);

        File.Exists(path: finalSegment).Should().BeTrue(because: "success must never delete output files");
    }
}
