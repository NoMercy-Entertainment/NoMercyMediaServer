namespace NoMercy.Tests.Encoder.Strategies;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Jobs;
using NoMercy.Encoder.Pipeline;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Progress;
using NoMercy.Encoder.Strategies.Hls;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

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
        _tempRoot = Path.Combine(Path.GetTempPath(), $"CancelTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        _encoder = new Mock<IEncoder>(MockBehavior.Strict);
        _checkpointStore = new Mock<ICheckpointStore>();
        _storage = TestStorageFactory.CreateLocal();

        _strategy = new HlsTwoPassStrategy(
            _encoder.Object,
            _checkpointStore.Object,
            NullLogger<HlsTwoPassStrategy>.Instance,
            _storage
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);

        GC.SuppressFinalize(this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — cancellation during pass 1 deletes partial output + checkpoint
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EncodeAsync_CancellationDuringPass1_DeletesPartialOutputAndCheckpoint()
    {
        string outputDir = Path.Combine(_tempRoot, "output_cancel");
        Directory.CreateDirectory(outputDir);

        // Seed a partial output file — simulates bytes written before cancel.
        string partialFile = Path.Combine(outputDir, "segment-000.ts");
        await File.WriteAllTextAsync(partialFile, "partial data");

        // Seed a checkpoint — simulates pass 1 having completed then cancel firing.
        string checkpointPath = Path.Combine(outputDir, ".checkpoint.json");
        await File.WriteAllTextAsync(checkpointPath, "{}");

        // No existing checkpoint in store — pass 1 must run.
        _checkpointStore
            .Setup(s => s.LoadAsync(outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        // The inner encoder throws OperationCanceledException when ct fires.
        using CancellationTokenSource cts = new();
        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (EncodingRequest _, IProgressObserver? _, CancellationToken ct) =>
                {
                    cts.Cancel();
                    await Task.Delay(1, ct); // yields — lets cancellation propagate
                    ct.ThrowIfCancellationRequested();
                    return new EncodingResult(true, outputDir, TimeSpan.Zero, null, null);
                }
            );

        // DeleteAsync on the checkpoint store must be called.
        _checkpointStore
            .Setup(s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        EncodingRequest request = BuildRequest(outputDir);

        Func<Task> act = async () =>
            await _strategy.EncodeAsync(request, progress: null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        // Checkpoint store Delete was called.
        _checkpointStore.Verify(
            s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Partial output file was deleted from the real filesystem.
        File.Exists(partialFile).Should().BeFalse("partial output must be removed on cancel");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — FFmpeg crash (non-zero exit) leaves checkpoint for resume
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EncodeAsync_FfmpegCrashes_CheckpointPersistsForResume()
    {
        string outputDir = Path.Combine(_tempRoot, "output_crash");
        Directory.CreateDirectory(outputDir);

        // No existing checkpoint — fresh encode.
        _checkpointStore
            .Setup(s => s.LoadAsync(outputDir, It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobCheckpoint?)null);

        // SaveAsync records the checkpoint (pass 1 succeeded).
        JobCheckpoint? savedCheckpoint = null;
        _checkpointStore
            .Setup(s => s.SaveAsync(It.IsAny<JobCheckpoint>(), It.IsAny<CancellationToken>()))
            .Callback<JobCheckpoint, CancellationToken>((cp, _) => savedCheckpoint = cp)
            .Returns(Task.CompletedTask);

        // Pass 1 succeeds.
        NoMercy.Encoder.Errors.EncodingError crashError = new(
            Kind: NoMercy.Encoder.Errors.EncodingErrorKind.ProcessCrashed,
            Message: "FFmpeg exited with code 1",
            FfmpegStderr: "Killed",
            StageName: "Execute",
            Recoverable: true
        );

        int callCount = 0;
        _encoder
            .Setup(e =>
                e.EncodeAsync(
                    It.IsAny<EncodingRequest>(),
                    It.IsAny<IProgressObserver?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call = pass 1 → success (checkpoint will be saved).
                // Second call = pass 2 → crash.
                return callCount == 1
                    ? new EncodingResult(true, outputDir, TimeSpan.FromSeconds(10), null, null)
                    : new EncodingResult(
                        false,
                        outputDir,
                        TimeSpan.FromSeconds(5),
                        crashError,
                        null
                    );
            });

        // DeleteAsync must NOT be called — crash should preserve checkpoint.
        _checkpointStore
            .Setup(s => s.DeleteAsync(outputDir, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        EncodingRequest request = BuildRequest(outputDir);

        EncodingResult result = await _strategy.EncodeAsync(
            request,
            progress: null,
            CancellationToken.None
        );

        result.Success.Should().BeFalse();

        // Checkpoint was saved after pass 1.
        savedCheckpoint.Should().NotBeNull("checkpoint must be saved after pass 1 for resume");
        savedCheckpoint!.Pass1Completed.Should().BeTrue();

        // DeleteAsync was NOT called — crash path must leave checkpoint intact.
        _checkpointStore.Verify(
            s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a crash must not delete the checkpoint — resume depends on it"
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static EncodingRequest BuildRequest(string outputDir) =>
        new(
            InputPath: "/media/source/movie.mkv",
            OutputDirectory: outputDir,
            Profile: new EncodingProfile(
                Id: Ulid.NewUlid(),
                Name: "Test HLS",
                Format: OutputFormat.Hls,
                VideoOutputs: [],
                AudioOutputs: [],
                SubtitleOutputs: []
            )
        );
}
