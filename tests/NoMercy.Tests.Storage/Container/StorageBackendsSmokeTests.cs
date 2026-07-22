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

using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Drivers.WebDav;

namespace NoMercy.Tests.Storage.Container;

// TEMPORARY end-to-end validation that the single all-in-one fixture brings up
// all three backends and each driver round-trips. Proves the fixture before the
// real test classes are re-pointed at it. Removed once the suite is converted.
[Collection(name: "StorageBackends")]
public sealed class StorageBackendsSmokeTests(StorageBackendsFixture fix)
{
    private void Require() => Skip.If(condition: !fix.Available, reason: fix.StartupError ?? "container unavailable");

    [SkippableFact]
    public async Task S3_round_trips()
    {
        Require();
        using S3StorageDriver driver = fix.BuildS3Driver();
        string path = $"smoke/{Ulid.NewUlid()}.bin";
        byte[] data = "hello s3"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);
        await using Stream r = driver.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);
        driver.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task WebDav_round_trips()
    {
        Require();
        using WebDavStorageDriver driver = fix.BuildWebDavDriver();
        string path = $"smoke-{Ulid.NewUlid()}.bin";
        byte[] data = "hello webdav"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);
        await using Stream r = driver.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);
        driver.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task Smb_round_trips()
    {
        Require();
        using SmbStorageDriver driver = fix.BuildSmbDriver();
        string path = $"smoke-{Ulid.NewUlid()}.bin";
        byte[] data = "hello smb"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);
        driver.FileExists(path: path).Should().BeTrue();
        await using Stream r = driver.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);
        driver.DeleteFile(path: path);
        driver.FileExists(path: path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Nfs_round_trips_and_sees_seeded_layout()
    {
        Require();
        NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(condition: driver is null, reason: "libnfs native library not installed");
        using NfsStorageDriver nfs = driver!;

        // Seeded NAS layout is visible (generic names exercising $/[]/space/UTF-8).
        nfs.DirectoryExists(path: "/Music").Should().BeTrue();
        nfs.DirectoryExists(path: "/Music/A/artist$/[2025] album$").Should().BeTrue();
        nfs.FileExists(path: "/Music/A/artist$/[2025] album$/01 track one.mp3").Should().BeTrue();

        // Round-trip a fresh file.
        string path = $"/smoke-{Ulid.NewUlid()}.bin";
        byte[] data = "hello nfs"u8.ToArray();
        await using (Stream w = nfs.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);
        await using Stream r = nfs.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);
        nfs.DeleteFile(path: path);
    }

    [SkippableFact]
    public void Container_generated_media_exists()
    {
        Require();
        File.Exists(path: Path.Combine(path1: fix.MediaDir, path2: "video-sd480p.mkv")).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: fix.MediaDir, path2: "video-1080p.mkv")).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: fix.MediaDir, path2: "video-hdr10.mkv")).Should().BeTrue();
        File.Exists(path: Path.Combine(path1: fix.MediaDir, path2: "audio-only.flac")).Should().BeTrue();
    }
}
