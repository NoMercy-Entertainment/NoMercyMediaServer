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
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            DrmConfig config = new(DrmMethod.Aes128, KeyUri: "https://example/key/abc");

            DrmArtifact artifact = await sut.PrepareAsync(dir, config, CancellationToken.None);
            artifactDir = Path.GetDirectoryName(artifact.KeyFilePath);

            artifact.Key.Length.Should().Be(16);
            artifact.Iv.Length.Should().Be(16);
            File.Exists(artifact.KeyFilePath).Should().BeTrue();
            File.Exists(artifact.KeyInfoFilePath).Should().BeTrue();

            byte[] onDisk = await File.ReadAllBytesAsync(artifact.KeyFilePath);
            onDisk.Should().Equal(artifact.Key);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            if (!string.IsNullOrEmpty(artifactDir) && Directory.Exists(artifactDir))
                Directory.Delete(artifactDir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_NeverWritesArtifacts_IntoOutputDirectory()
    {
        string outputDirectory = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            DrmConfig config = new(DrmMethod.Aes128, KeyUri: "https://example/key/no-leak");

            DrmArtifact artifact = await sut.PrepareAsync(
                outputDirectory,
                config,
                CancellationToken.None
            );

            // The output directory is what gets published to the served
            // destination — the raw key must never land there.
            File.Exists(Path.Combine(outputDirectory, "drm.key")).Should().BeFalse();
            File.Exists(Path.Combine(outputDirectory, "drm_keyinfo.txt")).Should().BeFalse();
            Directory
                .GetFileSystemEntries(outputDirectory)
                .Should()
                .BeEmpty("PrepareAsync must not write any artifact into outputDirectory");

            string fullArtifactDir = Path.GetFullPath(Path.GetDirectoryName(artifact.KeyFilePath)!);
            string fullTempRoot = Path.GetFullPath(StoragePaths.TempRoot);
            fullArtifactDir.Should().StartWith(fullTempRoot);

            Directory.Delete(fullArtifactDir, recursive: true);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_PersistsProtectedKey_RetrievableByKeyUri()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            string keyUri = $"https://example/key/{Guid.NewGuid():N}";
            DrmConfig config = new(DrmMethod.Aes128, KeyUri: keyUri);

            DrmArtifact artifact = await sut.PrepareAsync(dir, config, CancellationToken.None);

            (byte[] Key, byte[] Iv)? stored = await DrmKeyStore.TryGetKeyAsync(
                keyUri,
                CancellationToken.None
            );

            stored
                .Should()
                .NotBeNull(
                    "the raw key must be recoverable for an authorized key-serving endpoint"
                );
            stored!.Value.Key.Should().Equal(artifact.Key);
            stored.Value.Iv.Should().Equal(artifact.Iv);

            Directory.Delete(Path.GetDirectoryName(artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_KeyInfoFile_HasFfmpegFormat()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            byte[] fixedKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            byte[] fixedIv = Enumerable.Range(16, 16).Select(i => (byte)i).ToArray();
            DrmConfig config = new(
                DrmMethod.Aes128,
                KeyUri: "https://example/k/42",
                Key: fixedKey,
                Iv: fixedIv
            );

            DrmArtifact artifact = await sut.PrepareAsync(dir, config, CancellationToken.None);

            string contents = await File.ReadAllTextAsync(artifact.KeyInfoFilePath);
            string[] lines = contents.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            lines[0].Should().Be("https://example/k/42");
            lines[1].Should().EndWith("/drm.key");
            lines[2].Should().Be(Convert.ToHexString(fixedIv).ToLowerInvariant());

            Directory.Delete(Path.GetDirectoryName(artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_AcceptsCallerProvidedKey()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            byte[] key = Enumerable.Range(0, 16).Select(i => (byte)(0xA0 + i)).ToArray();
            DrmConfig config = new(DrmMethod.Aes128, KeyUri: "http://k", Key: key);

            DrmArtifact artifact = await sut.PrepareAsync(dir, config, CancellationToken.None);

            artifact.Key.Should().Equal(key);
            byte[] onDisk = await File.ReadAllBytesAsync(artifact.KeyFilePath);
            onDisk.Should().Equal(key);

            Directory.Delete(Path.GetDirectoryName(artifact.KeyFilePath)!, recursive: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsWrongKeyLength()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            DrmConfig config = new(
                DrmMethod.Aes128,
                KeyUri: "http://k",
                Key: new byte[8] // too short
            );

            Func<Task> act = () => sut.PrepareAsync(dir, config, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*key must be 16*");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsWrongMethod()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            DrmConfig config = new(DrmMethod.None, KeyUri: "http://k");

            Func<Task> act = () => sut.PrepareAsync(dir, config, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*AES-128 only*");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareAsync_RejectsEmptyKeyUri()
    {
        string dir = NewTempDir();
        try
        {
            Aes128HlsDrmProcessor sut = new(TestStorageFactory.CreateLocal());
            DrmConfig config = new(DrmMethod.Aes128, KeyUri: "");

            Func<Task> act = () => sut.PrepareAsync(dir, config, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*KeyUri*");
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Method_IsAes128() =>
        new Aes128HlsDrmProcessor(TestStorageFactory.CreateLocal())
            .Method.Should()
            .Be(DrmMethod.Aes128);

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"drm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
