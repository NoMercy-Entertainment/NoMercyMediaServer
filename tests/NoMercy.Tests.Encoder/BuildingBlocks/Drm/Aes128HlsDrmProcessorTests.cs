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

using NoMercy.Encoder.BuildingBlocks.Drm;
using NoMercy.NmSystem.Security;
using NoMercy.Storage;
using NoMercy.Tests.Encoder.Storage;

namespace NoMercy.Tests.Encoder.BuildingBlocks.Drm;

public class Aes128HlsDrmProcessorTests
{
    [Fact]
    public async Task PrepareAsync_GeneratesRandomKeyAndIv_WhenNoneProvided()
    {
        string dir = NewTempDir();
        string? artifactDir = null;
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            DrmConfig config = new(Method: DrmMethod.Aes128, KeyUri: "https://example/key/abc");

            DrmArtifact artifact = await sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);
            artifactDir = Path.GetDirectoryName(path: artifact.KeyFilePath);

            artifact.Key.Length.Should().Be(expected: 16);
            artifact.Iv.Length.Should().Be(expected: 16);
            File.Exists(path: artifact.KeyFilePath).Should().BeTrue();
            File.Exists(path: artifact.KeyInfoFilePath).Should().BeTrue();

            byte[] onDisk = await File.ReadAllBytesAsync(path: artifact.KeyFilePath);
            onDisk.Should().Equal(elements: artifact.Key);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
            if (!string.IsNullOrEmpty(value: artifactDir) && Directory.Exists(path: artifactDir))
                Directory.Delete(path: artifactDir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_NeverWritesArtifacts_IntoOutputDirectory()
    {
        string outputDirectory = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            DrmConfig config = new(Method: DrmMethod.Aes128, KeyUri: "https://example/key/no-leak");

            DrmArtifact artifact = await sut.PrepareAsync(
                outputDirectory: outputDirectory,
                config: config,
                ct: CancellationToken.None
            );

            // The output directory is what gets published to the served
            // destination — the raw key must never land there.
            File.Exists(path: Path.Combine(path1: outputDirectory, path2: "drm.key")).Should().BeFalse();
            File.Exists(path: Path.Combine(path1: outputDirectory, path2: "drm_keyinfo.txt")).Should().BeFalse();
            Directory
                .GetFileSystemEntries(path: outputDirectory)
                .Should()
                .BeEmpty(because: "PrepareAsync must not write any artifact into outputDirectory");

            string fullArtifactDir = Path.GetFullPath(path: Path.GetDirectoryName(path: artifact.KeyFilePath)!);
            string fullTempRoot = Path.GetFullPath(path: StoragePaths.TempRoot);
            fullArtifactDir.Should().StartWith(expected: fullTempRoot);

            Directory.Delete(path: fullArtifactDir, recursive: true);
        }
        finally
        {
            Directory.Delete(path: outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_PersistsProtectedKey_RetrievableByKeyUri()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            string keyUri = $"https://example/key/{Guid.NewGuid():N}";
            DrmConfig config = new(Method: DrmMethod.Aes128, KeyUri: keyUri);

            DrmArtifact artifact = await sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            (byte[] Key, byte[] Iv)? stored = await DrmKeyStore.TryGetKeyAsync(
                keyUri: keyUri,
                ct: CancellationToken.None
            );

            stored
                .Should()
                .NotBeNull(
                    because: "the raw key must be recoverable for an authorized key-serving endpoint"
                );
            stored!.Value.Key.Should().Equal(elements: artifact.Key);
            stored.Value.Iv.Should().Equal(elements: artifact.Iv);

            Directory.Delete(path: Path.GetDirectoryName(path: artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_KeyInfoFile_HasFfmpegFormat()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            byte[] fixedKey = Enumerable.Range(start: 0, count: 16).Select(selector: i => (byte)i).ToArray();
            byte[] fixedIv = Enumerable.Range(start: 16, count: 16).Select(selector: i => (byte)i).ToArray();
            DrmConfig config = new(
                Method: DrmMethod.Aes128,
                KeyUri: "https://example/k/42",
                Key: fixedKey,
                Iv: fixedIv
            );

            DrmArtifact artifact = await sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            string contents = await File.ReadAllTextAsync(path: artifact.KeyInfoFilePath);
            string[] lines = contents.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries);

            lines[0].Should().Be(expected: "https://example/k/42");
            lines[1].Should().EndWith(expected: "/drm.key");
            lines[2].Should().Be(expected: Convert.ToHexString(inArray: fixedIv).ToLowerInvariant());

            Directory.Delete(path: Path.GetDirectoryName(path: artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_AcceptsCallerProvidedKey()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            byte[] key = Enumerable.Range(start: 0, count: 16).Select(selector: i => (byte)(0xA0 + i)).ToArray();
            DrmConfig config = new(Method: DrmMethod.Aes128, KeyUri: "http://k", Key: key);

            DrmArtifact artifact = await sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            artifact.Key.Should().Equal(elements: key);
            byte[] onDisk = await File.ReadAllBytesAsync(path: artifact.KeyFilePath);
            onDisk.Should().Equal(elements: key);

            Directory.Delete(path: Path.GetDirectoryName(path: artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsWrongKeyLength()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            DrmConfig config = new(
                Method: DrmMethod.Aes128,
                KeyUri: "http://k",
                Key: new byte[8] // too short
            );

            Func<Task> act = () => sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedWildcardPattern: "*key must be 16*");
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsWrongMethod()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            DrmConfig config = new(Method: DrmMethod.None, KeyUri: "http://k");

            Func<Task> act = () => sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedWildcardPattern: "*AES-128 only*");
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsEmptyKeyUri()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(storage: TestStorageFactory.CreateLocal());
            DrmConfig config = new(Method: DrmMethod.Aes128, KeyUri: "");

            Func<Task> act = () => sut.PrepareAsync(outputDirectory: dir, config: config, ct: CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage(expectedWildcardPattern: "*KeyUri*");
        }
        finally
        {
            if (Directory.Exists(path: dir))
                Directory.Delete(path: dir, recursive: true);
        }
    }

    [Fact]
    public void Method_IsAes128() =>
        new Aes128HlsDrmProcessor(storage: TestStorageFactory.CreateLocal())
            .Method.Should()
            .Be(expected: DrmMethod.Aes128);

    private static string NewTempDir()
    {
        string dir = Path.Combine(path1: Path.GetTempPath(), path2: $"drm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: dir);
        return dir;
    }
}
