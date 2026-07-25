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

using NoMercy.Providers.Helpers;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Providers.Helpers;

public class CacheControllerTests : IDisposable
{
    private readonly string _testCacheDir;

    public CacheControllerTests()
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), $"CacheControllerTest_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_testCacheDir);

        LocalStorageDriver driver = new();
        // Scope to the system temp root so both _testCacheDir and any
        // nonExistent sibling paths (also under temp) pass the allowlist check.
        StoragePathGuard guard = new([Path.GetTempPath()], driver);
        IStorage storage = new LocalStorage(driver, guard);
        CacheController.Initialize(storage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testCacheDir))
        {
            Directory.Delete(_testCacheDir, recursive: true);
        }
    }

    [Fact]
    public void PruneCache_DeletesOldestFiles_WhenExceedingSizeLimit()
    {
        // Arrange: create 5 files, each 200 bytes, total = 1000 bytes
        // Set max to 500 bytes so pruning should delete the oldest files
        List<string> createdFiles = [];

        for (int i = 0; i < 5; i++)
        {
            string filePath = Path.Combine(_testCacheDir, $"file_{i}.txt");
            byte[] data = new byte[200];
            Array.Fill(data, (byte)'A');
            File.WriteAllBytes(filePath, data);

            // PruneCache orders by StorageEntry.LastModified, which the facade
            // derives from File.GetLastWriteTimeUtc. Stagger write times so the
            // oldest-first deletion order is deterministic.
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(-10 + i));
            createdFiles.Add(filePath);
        }

        // Act: prune with 500-byte limit
        CacheController.PruneCache(_testCacheDir, maxSizeBytes: 500);

        // Assert: oldest files deleted, newest kept
        // Total was 1000, limit is 500, so at least 3 files should be deleted
        // (each 200 bytes: delete 3 => 400 remaining <= 500)
        string[] remaining = Directory.GetFiles(_testCacheDir);
        long remainingSize = remaining.Sum(f => new FileInfo(f).Length);

        Assert.True(
            remainingSize <= 500,
            $"Remaining cache size {remainingSize} exceeds limit of 500 bytes"
        );

        // The oldest files (file_0, file_1, file_2) should be deleted
        Assert.False(File.Exists(createdFiles[0]), "Oldest file should be deleted");
        Assert.False(File.Exists(createdFiles[1]), "Second oldest file should be deleted");
        Assert.False(File.Exists(createdFiles[2]), "Third oldest file should be deleted");

        // Newest files should remain
        Assert.True(File.Exists(createdFiles[3]), "Newer file should be kept");
        Assert.True(File.Exists(createdFiles[4]), "Newest file should be kept");
    }

    [Fact]
    public void PruneCache_DoesNothing_WhenUnderSizeLimit()
    {
        // Arrange: create 2 files, each 100 bytes, total = 200 bytes
        for (int i = 0; i < 2; i++)
        {
            string filePath = Path.Combine(_testCacheDir, $"file_{i}.txt");
            byte[] data = new byte[100];
            File.WriteAllBytes(filePath, data);
        }

        // Act: prune with 500-byte limit (under limit)
        CacheController.PruneCache(_testCacheDir, maxSizeBytes: 500);

        // Assert: all files remain
        Assert.Equal(2, Directory.GetFiles(_testCacheDir).Length);
    }

    [Fact]
    public void PruneCache_HandlesEmptyDirectory()
    {
        // Act & Assert: should not throw
        CacheController.PruneCache(_testCacheDir, maxSizeBytes: 500);

        Assert.Empty(Directory.GetFiles(_testCacheDir));
    }

    [Fact]
    public void PruneCache_HandlesNonExistentDirectory()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"NonExistent_{Guid.NewGuid():N}");

        // Act & Assert: should not throw
        CacheController.PruneCache(nonExistent, maxSizeBytes: 500);
    }

    [Fact]
    public void GenerateFileName_ReturnsDeterministicHash()
    {
        string url = "https://api.themoviedb.org/3/movie/123";

        string hash1 = CacheController.GenerateFileName(url);
        string hash2 = CacheController.GenerateFileName(url);

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void GenerateFileName_ReturnsDifferentHashForDifferentUrls()
    {
        string hash1 = CacheController.GenerateFileName("https://api.example.com/a");
        string hash2 = CacheController.GenerateFileName("https://api.example.com/b");

        Assert.NotEqual(hash1, hash2);
    }
}
