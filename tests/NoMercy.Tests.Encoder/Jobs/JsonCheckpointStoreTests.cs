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
using NoMercy.Encoder.Jobs;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.Jobs;

public class JsonCheckpointStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonCheckpointStore _store;

    public JsonCheckpointStoreTests()
    {
        _tempDir = Path.Combine(path1: Path.GetTempPath(), path2: $"CheckpointStore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _tempDir);

        _store = new(storage: TestStorageFactory.CreateLocal(), logger: NullLogger<JsonCheckpointStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(path: _tempDir))
            Directory.Delete(path: _tempDir, recursive: true);

        GC.SuppressFinalize(obj: this);
    }

    [Fact]
    public async Task Save_WritesCheckpointFileInOutputDirectory()
    {
        JobCheckpoint checkpoint = Build(outputDirectory: _tempDir);

        await _store.SaveAsync(checkpoint: checkpoint);

        string expectedPath = Path.Combine(path1: _tempDir, path2: ".checkpoint.json");
        File.Exists(path: expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task Load_RoundTripsAllFields()
    {
        JobCheckpoint original = Build(
            outputDirectory: _tempDir,
            statsFilePath: "/tmp/x264-pass.log",
            pass1Completed: true,
            lastCompletedSegment: 42,
            encodeMode: "TwoPass"
        );

        await _store.SaveAsync(checkpoint: original);
        JobCheckpoint? loaded = await _store.LoadAsync(outputDirectory: _tempDir);

        loaded.Should().NotBeNull();
        loaded!.JobId.Should().Be(expected: original.JobId);
        loaded.StatsFilePath.Should().Be(expected: "/tmp/x264-pass.log");
        loaded.Pass1Completed.Should().BeTrue();
        loaded.LastCompletedSegment.Should().Be(expected: 42);
        loaded.EncodeMode.Should().Be(expected: "TwoPass");
    }

    [Fact]
    public async Task Load_WhenFileMissing_ReturnsNull()
    {
        JobCheckpoint? loaded = await _store.LoadAsync(outputDirectory: _tempDir);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Load_WhenFileCorrupt_ReturnsNull()
    {
        string path = Path.Combine(path1: _tempDir, path2: ".checkpoint.json");
        await File.WriteAllTextAsync(path: path, contents: "{ this is not valid json");

        JobCheckpoint? loaded = await _store.LoadAsync(outputDirectory: _tempDir);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_UpdatesLastUpdatedTimestamp()
    {
        DateTime before = DateTime.UtcNow;
        JobCheckpoint checkpoint = Build(outputDirectory: _tempDir) with
        {
            LastUpdated = new(year: 2020, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc),
        };

        await _store.SaveAsync(checkpoint: checkpoint);
        JobCheckpoint? loaded = await _store.LoadAsync(outputDirectory: _tempDir);

        loaded!.LastUpdated.Should().BeOnOrAfter(expected: before);
    }

    [Fact]
    public async Task Delete_RemovesCheckpointFile()
    {
        JobCheckpoint checkpoint = Build(outputDirectory: _tempDir);
        await _store.SaveAsync(checkpoint: checkpoint);

        await _store.DeleteAsync(outputDirectory: _tempDir);

        string path = Path.Combine(path1: _tempDir, path2: ".checkpoint.json");
        File.Exists(path: path).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WhenFileMissing_DoesNotThrow()
    {
        await _store.DeleteAsync(outputDirectory: _tempDir);

        // No assertion needed — reaching this line without exception is the test.
        true.Should().BeTrue();
    }

    [Fact]
    public async Task Save_CreatesOutputDirectoryIfMissing()
    {
        string missingDir = Path.Combine(path1: _tempDir, path2: "nested", path3: "output");
        JobCheckpoint checkpoint = Build(outputDirectory: missingDir);

        await _store.SaveAsync(checkpoint: checkpoint);

        Directory.Exists(path: missingDir).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: missingDir, path2: ".checkpoint.json")).Should().BeTrue();
    }

    private static JobCheckpoint Build(
        string outputDirectory,
        string? statsFilePath = null,
        bool pass1Completed = false,
        int lastCompletedSegment = -1,
        string? encodeMode = null
    ) =>
        new(
            JobId: "job-" + Guid.NewGuid().ToString(format: "N")[..8],
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
