using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Jobs;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Jobs;

public class JsonCheckpointStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonCheckpointStore _store;

    public JsonCheckpointStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CheckpointStore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store = new(TestStorageFactory.CreateLocal(), NullLogger<JsonCheckpointStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Save_WritesCheckpointFileInOutputDirectory()
    {
        JobCheckpoint checkpoint = Build(_tempDir);

        await _store.SaveAsync(checkpoint);

        string expectedPath = Path.Combine(_tempDir, ".checkpoint.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Load_RoundTripsAllFields()
    {
        JobCheckpoint original = Build(
            _tempDir,
            statsFilePath: "/tmp/x264-pass.log",
            pass1Completed: true,
            lastCompletedSegment: 42,
            encodeMode: "TwoPass"
        );

        await _store.SaveAsync(original);
        JobCheckpoint? loaded = await _store.LoadAsync(_tempDir);

        loaded.Should().NotBeNull();
        loaded!.JobId.Should().Be(original.JobId);
        loaded.StatsFilePath.Should().Be("/tmp/x264-pass.log");
        loaded.Pass1Completed.Should().BeTrue();
        loaded.LastCompletedSegment.Should().Be(42);
        loaded.EncodeMode.Should().Be("TwoPass");
    }

    [Fact]
    public async Task Load_WhenFileMissing_ReturnsNull()
    {
        JobCheckpoint? loaded = await _store.LoadAsync(_tempDir);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Load_WhenFileCorrupt_ReturnsNull()
    {
        string path = Path.Combine(_tempDir, ".checkpoint.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        JobCheckpoint? loaded = await _store.LoadAsync(_tempDir);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_UpdatesLastUpdatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;
        JobCheckpoint checkpoint = Build(_tempDir) with
        {
            LastUpdated = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await _store.SaveAsync(checkpoint);
        JobCheckpoint? loaded = await _store.LoadAsync(_tempDir);

        loaded!.LastUpdated.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task Delete_RemovesCheckpointFile()
    {
        JobCheckpoint checkpoint = Build(_tempDir);
        await _store.SaveAsync(checkpoint);

        await _store.DeleteAsync(_tempDir);

        string path = Path.Combine(_tempDir, ".checkpoint.json");
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WhenFileMissing_DoesNotThrow()
    {
        await _store.DeleteAsync(_tempDir);

        // No assertion needed — reaching this line without exception is the test.
        true.Should().BeTrue();
    }

    [Fact]
    public async Task Save_CreatesOutputDirectoryIfMissing()
    {
        string missingDir = Path.Combine(_tempDir, "nested", "output");
        JobCheckpoint checkpoint = Build(missingDir);

        await _store.SaveAsync(checkpoint);

        Directory.Exists(missingDir).Should().BeTrue();
        File.Exists(Path.Combine(missingDir, ".checkpoint.json")).Should().BeTrue();
    }

    private static JobCheckpoint Build(
        string outputDirectory,
        string? statsFilePath = null,
        bool pass1Completed = false,
        int lastCompletedSegment = -1,
        string? encodeMode = null
    ) =>
        new(
            JobId: "job-" + Guid.NewGuid().ToString("N")[..8],
            InputPath: "/media/source.mkv",
            OutputDirectory: outputDirectory,
            CompletedGroupIndices: [0, 1],
            LastUpdated: DateTime.UtcNow,
            StatsFilePath: statsFilePath,
            Pass1Completed: pass1Completed,
            LastCompletedSegment: lastCompletedSegment,
            EncodeMode: encodeMode
        );
}
