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
/// Verifies items 613-614 of the encoder v3 alignment plan:
///   613 — on user cancellation, partial output is deleted.
///   614 — on user cancellation, no checkpoint file survives.
/// A paired test verifies the opposite: an FFmpeg crash (non-zero exit from
/// the inner encoder) DOES leave the checkpoint so resume can work.
/// </summary>
public class CancellationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly Mock<IEncoder> _encoder;
    private readonly Mock<ICheckpointStore> _checkpointStore;
    private readonly LocalStorage _storage;

    // A minimal HLS strategy (concrete subclass) driven by mocked dependencies.
    private readonly HlsTwoPassStrategy _strategy;

    public CancellationTests()
    {
        _tempRoot = Path.Combine(path1: Path.GetTempPath(), path2: $"CancelTest_{Guid.NewGuid():N}");
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

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — cancellation during pass 1 deletes partial output + checkpoint
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EncodeAsync_CancellationDuringPass1_DeletesPartialOutputAndCheckpoint()
    {
        string outputDir = Path.Combine(path1: _tempRoot, path2: "output_cancel");
        Directory.CreateDirectory(path: outputDir);

        // Seed a partial output file — simulates bytes written before cancel.
        string partialFile = Path.Combine(path1: outputDir, path2: "segment-000.ts");
        await File.WriteAllTextAsync(path: partialFile, contents: "partial data");

        // Seed a checkpoint — simulates pass 1 having completed then cancel firing.
        string checkpointPath = Path.Combine(path1: outputDir, path2: ".checkpoint.json");
        await File.WriteAllTextAsync(path: checkpointPath, contents: "{}");

        // No existing checkpoint in store — pass 1 must run.
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        // The inner encoder throws OperationCanceledException when ct fires.
        using CancellationTokenSource cts = new();
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                valueFunction: async (EncodingRequest _, IProgressObserver? _, CancellationToken ct) =>
                {
                    cts.Cancel();
                    await Task.Delay(millisecondsDelay: 1, cancellationToken: ct); // yields — lets cancellation propagate
                    ct.ThrowIfCancellationRequested();
                    return new(Success: true, OutputPath: outputDir, Duration: TimeSpan.Zero, Error: null, Metrics: null);
                }
            );

        // DeleteAsync on the checkpoint store must be called.
        _checkpointStore
            .Setup(expression: s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask)
            .Verifiable();

        EncodingRequest request = BuildRequest(outputDir: outputDir);

        Func<Task> act = async () =>
            await _strategy.EncodeAsync(request: request, progress: null, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // Checkpoint store Delete was called.
        _checkpointStore.Verify(
            expression: s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()),
            times: Times.Once
        );

        // Partial output file was deleted from the real filesystem.
        File.Exists(path: partialFile).Should().BeFalse(because: "partial output must be removed on cancel");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — FFmpeg crash (non-zero exit) leaves checkpoint for resume
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EncodeAsync_FfmpegCrashes_CheckpointPersistsForResume()
    {
        string outputDir = Path.Combine(path1: _tempRoot, path2: "output_crash");
        Directory.CreateDirectory(path: outputDir);

        // No existing checkpoint — fresh encode.
        _checkpointStore
            .Setup(expression: s => s.LoadAsync(outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: (JobCheckpoint?)null);

        // SaveAsync records the checkpoint (pass 1 succeeded).
        JobCheckpoint? savedCheckpoint = null;
        _checkpointStore
            .Setup(expression: s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>(action: (cp, _) => savedCheckpoint = cp)
            .Returns(value: Task.CompletedTask);

        // Pass 1 succeeds.
        EncodingError crashError = new(
            Kind: EncodingErrorKind.ProcessCrashed,
            Message: "FFmpeg exited with code 1",
            FfmpegStderr: "Killed",
            StageName: "Execute",
            Recoverable: true
        );

        int callCount = 0;
        _encoder
            .Setup(expression: e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(valueFunction: () =>
            {
                callCount++;
                // First call = pass 1 → success (checkpoint will be saved).
                // Second call = pass 2 → crash.
                return callCount == 1
                    ? new(Success: true, OutputPath: outputDir, Duration: TimeSpan.FromSeconds(seconds: 10), Error: null, Metrics: null)
                    : new EncodingResult(
                        Success: false,
                        OutputPath: outputDir,
                        Duration: TimeSpan.FromSeconds(seconds: 5),
                        Error: crashError,
                        Metrics: null
                    );
            });

        // DeleteAsync must NOT be called — crash should preserve checkpoint.
        _checkpointStore
            .Setup(expression: s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()))
            .Returns(value: Task.CompletedTask);

        EncodingRequest request = BuildRequest(outputDir: outputDir);

        EncodingResult result = await _strategy.EncodeAsync(
            request: request,
            progress: null,
            ct: CancellationToken.None
        );

        result.Success.Should().BeFalse();

        // Checkpoint was saved after pass 1.
        savedCheckpoint.Should().NotBeNull(because: "checkpoint must be saved after pass 1 for resume");
        savedCheckpoint!.Pass1Completed.Should().BeTrue();

        // DeleteAsync was NOT called — crash path must leave checkpoint intact.
        _checkpointStore.Verify(
            expression: s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            times: Times.Never,
            failMessage: "a crash must not delete the checkpoint — resume depends on it"
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static EncodingRequest BuildRequest(string outputDir) =>
        new(
            InputPath: "/media/source/movie.mkv",
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
}
