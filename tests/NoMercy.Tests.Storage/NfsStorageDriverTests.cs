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

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Factory;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class NfsDriverConfigParsingTests
{
    [Fact]
    public void Parse_missing_server_throws()
    {
        Action act = () => NfsDriverConfig.Parse(json: """{"export":"/data"}""", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*server*");
    }

    [Fact]
    public void Parse_missing_export_throws()
    {
        Action act = () => NfsDriverConfig.Parse(json: """{"server":"192.168.1.1"}""", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*export*");
    }

    [Fact]
    public void Parse_invalid_version_throws()
    {
        Action act = () =>
            NfsDriverConfig.Parse(
                json: """{"server":"192.168.1.1","export":"/data","version":2}""",
                folderId: Ulid.NewUlid()
            );
        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*version*");
    }

    [Fact]
    public void Parse_null_config_throws()
    {
        Action act = () => NfsDriverConfig.Parse(json: null!, folderId: Ulid.NewUlid());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException()
    {
        Action act = () => NfsDriverConfig.Parse(json: "{{bad json", folderId: Ulid.NewUlid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_defaults_version_to_3()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"/media"}""",
            folderId: Ulid.NewUlid()
        );
        config.Version.Should().Be(expected: 3);
    }

    [Fact]
    public void Parse_defaults_port_to_2049()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"/media"}""",
            folderId: Ulid.NewUlid()
        );
        config.Port.Should().Be(expected: 2049);
    }

    [Fact]
    public void Parse_accepts_version_4()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"/media","version":4}""",
            folderId: Ulid.NewUlid()
        );
        config.Version.Should().Be(expected: 4);
    }

    [Fact]
    public void Parse_accepts_uid_gid()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"/media","uid":1000,"gid":1000}""",
            folderId: Ulid.NewUlid()
        );
        config.Uid.Should().Be(expected: 1000);
        config.Gid.Should().Be(expected: 1000);
    }

    [Fact]
    public void Parse_normalizes_export_leading_slash()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"media/files"}""",
            folderId: Ulid.NewUlid()
        );
        config.Export.Should().StartWith(expected: "/");
    }

    [Fact]
    public void Parse_trims_trailing_slash_from_export()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            json: """{"server":"nas.local","export":"/media/"}""",
            folderId: Ulid.NewUlid()
        );
        config.Export.Should().Be(expected: "/media");
    }

    private static StorageFactory FactoryWithConfig(string type, string? config)
    {
        Mock<IDriverConfigResolver> resolver = new();
        resolver.Setup(expression: r => r.Resolve(It.IsAny<Ulid>())).Returns(value: (type, config));
        return new(driver: new LocalStorageDriver(), logger: NullLogger<StorageFactory>.Instance, driverConfigResolver: resolver.Object);
    }

    [Fact]
    public void StorageFactory_nfs_without_config_throws()
    {
        StorageFactory factory = FactoryWithConfig(type: "nfs", config: null);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StorageFactory_nfs_without_server_throws()
    {
        StorageFactory factory = FactoryWithConfig(type: "nfs", config: """{"export":"/data"}""");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*server*");
    }

    [Fact]
    public void StorageFactory_nfs_without_export_throws()
    {
        StorageFactory factory = FactoryWithConfig(type: "nfs", config: """{"server":"nas.local"}""");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(folderId: Ulid.NewUlid(), driverId: driverId, subPath: "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage(expectedWildcardPattern: "*export*");
    }
}

// ============================================================================
// Integration tests — run against the shared all-in-one storage container
// (NFS). The container is started once for the whole assembly by the
// StorageBackends collection fixture and torn down after the last test.
// ============================================================================

[Collection(name: "StorageBackends")]
public class NfsStorageDriverIntegrationTests(StorageBackendsFixture fix)
{
    private string SkipReason =>
        fix.NfsUnavailableReason
        ?? fix.StartupError
        ?? "storage container not available or libnfs native library not installed";

    /// <summary>
    /// Returns a backend instance, or signals skip if the fixture is unavailable
    /// (no Docker, container failed, or libnfs.dll/.so/.dylib not found).
    /// </summary>
    private NfsStorageDriver RequireBackend()
    {
        NfsStorageDriver? backend = fix.TryBuildNfsDriver();
        Skip.If(condition: backend is null, reason: SkipReason);
        return backend!;
    }

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/roundtrip-{Ulid.NewUlid()}.txt";
        byte[] data = "hello nfs"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        await using Stream r = backend.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().Equal(elements: data);

        backend.DeleteFile(path: path);
        backend.FileExists(path: path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task LargeFile_write_read()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/large-{Ulid.NewUlid()}.bin";

        byte[] data = new byte[11 * 1024 * 1024]; // 11 MB
        new Random(Seed: 99).NextBytes(buffer: data);

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.GetFileSize(path: path).Should().Be(expected: data.Length);

        await using Stream r = backend.OpenRead(path: path);
        using MemoryStream ms = new();
        await r.CopyToAsync(destination: ms);
        ms.ToArray().Should().HaveCount(expected: data.Length);

        backend.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using NfsStorageDriver backend = RequireBackend();
        string dir = $"/enum-{Ulid.NewUlid()}";
        backend.CreateDirectory(path: dir);

        string fileA = dir + "/a.txt";
        string fileB = dir + "/b.txt";
        string fileC = dir + "/c.bin";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: fileA, overwrite: true))
            await w.WriteAsync(buffer: bytes);
        await using (Stream w = backend.OpenWrite(path: fileB, overwrite: true))
            await w.WriteAsync(buffer: bytes);
        await using (Stream w = backend.OpenWrite(path: fileC, overwrite: true))
            await w.WriteAsync(buffer: bytes);

        IEnumerable<string> txtFiles = backend.EnumerateFileSystemEntries(
            directory: dir,
            searchPattern: "*.txt",
            option: SearchOption.AllDirectories
        );

        txtFiles.Should().HaveCount(expected: 2);

        backend.DeleteDirectory(path: dir, recursive: true);
        backend.DirectoryExists(path: dir).Should().BeFalse();
    }

    [SkippableFact]
    public async Task MoveFile_renames_path()
    {
        using NfsStorageDriver backend = RequireBackend();
        string src = $"/move-src-{Ulid.NewUlid()}.txt";
        string dst = $"/move-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.MoveFile(source: src, destination: dst);

        backend.FileExists(path: src).Should().BeFalse();
        backend.FileExists(path: dst).Should().BeTrue();

        backend.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_content()
    {
        using NfsStorageDriver backend = RequireBackend();
        string src = $"/copy-src-{Ulid.NewUlid()}.txt";
        string dst = $"/copy-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path: src, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.CopyFile(source: src, destination: dst, overwrite: true);

        backend.FileExists(path: src).Should().BeTrue();
        backend.FileExists(path: dst).Should().BeTrue();

        backend.DeleteFile(path: src);
        backend.DeleteFile(path: dst);
    }

    [SkippableFact]
    public async Task MkDir_and_RmDir_roundtrip()
    {
        using NfsStorageDriver backend = RequireBackend();
        string dir = $"/mkdir-test-{Ulid.NewUlid()}";

        backend.CreateDirectory(path: dir);
        backend.DirectoryExists(path: dir).Should().BeTrue();

        backend.DeleteDirectory(path: dir, recursive: false);
        backend.DirectoryExists(path: dir).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Mkdir_recursive_creates_nested_dirs()
    {
        using NfsStorageDriver backend = RequireBackend();
        string nested = $"/mkdir-{Ulid.NewUlid()}/sub/deep";

        backend.CreateDirectory(path: nested);
        backend.DirectoryExists(path: nested).Should().BeTrue();

        string top = nested.Split(separator: '/')[1];
        backend.DeleteDirectory(path: "/" + top, recursive: true);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/nooverwrite-{Ulid.NewUlid()}.txt";

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: "original"u8.ToArray());

        Action act = () => backend.OpenWrite(path: path, overwrite: false);
        act.Should().Throw<IOException>().WithMessage(expectedWildcardPattern: "*overwrite*");

        backend.DeleteFile(path: path);
    }

    [SkippableFact]
    public async Task GetFileSize_and_GetLastWriteTimeUtc()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/meta-{Ulid.NewUlid()}.txt";
        byte[] data = Encoding.UTF8.GetBytes(s: "metadata test");

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        backend.GetFileSize(path: path).Should().Be(expected: data.Length);

        DateTime mtime = backend.GetLastWriteTimeUtc(path: path);
        mtime.Should().BeCloseTo(nearbyTime: DateTime.UtcNow, precision: TimeSpan.FromMinutes(minutes: 2));

        backend.DeleteFile(path: path);
    }

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using NfsStorageDriver backend = RequireBackend();
        backend.IsHidden(path: ".hidden").Should().BeTrue();
        backend.IsHidden(path: "visible.txt").Should().BeFalse();
        backend.IsHidden(path: ".").Should().BeFalse();
    }

    [SkippableFact]
    public void Auth_with_uid_gid_connects_successfully()
    {
        Skip.If(condition: !fix.Available, reason: SkipReason);

        // The shared container exports NFSv4 at "/" with all_squash — everyone
        // maps to root, so uid/gid specifics don't apply. Connect with the
        // fixture defaults and assert the mount is visible.
        NfsDriverConfig config = NfsDriverConfig.For(
            server: StorageBackendsFixture.NfsHost,
            export: StorageBackendsFixture.NfsExport,
            version: 4
        );

        NfsStorageDriver? backend;
        try
        {
            backend = new(config: config);
        }
        catch (DllNotFoundException)
        {
            Skip.If(condition: true, reason: SkipReason);
            return;
        }

        using NfsStorageDriver b = backend;
        b.DirectoryExists(path: "/").Should().BeTrue();
    }

    /// <summary>
    /// Regression for the libnfs-context concurrency crash: NfsReadStream
    /// used to call LibNfs.Read on the shared context without acquiring the
    /// driver lock. Multiple parallel readers corrupted libnfs state and
    /// crashed the process with 0xC0000005. This test spins up several
    /// concurrent readers against the same driver and asserts every one
    /// returns the full file without throwing.
    /// </summary>
    [SkippableFact]
    public async Task ConcurrentReads_do_not_crash()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/concurrent-{Ulid.NewUlid()}.bin";

        // 2 MB payload — large enough to span many 32 KB libnfs read chunks
        // per reader so the contexts heavily interleave under the lock.
        byte[] data = new byte[2 * 1024 * 1024];
        new Random(Seed: 42).NextBytes(buffer: data);

        await using (Stream w = backend.OpenWrite(path: path, overwrite: true))
            await w.WriteAsync(buffer: data);

        try
        {
            const int readerCount = 6;
            Task<byte[]>[] readers = new Task<byte[]>[readerCount];
            for (int i = 0; i < readerCount; i++)
            {
                readers[i] = Task.Run(function: () =>
                {
                    using Stream r = backend.OpenRead(path: path);
                    using MemoryStream ms = new();
                    r.CopyTo(destination: ms);
                    return ms.ToArray();
                });
            }

            byte[][] results = await Task.WhenAll(tasks: readers);
            foreach (byte[] result in results)
                result.Should().Equal(elements: data);
        }
        finally
        {
            backend.DeleteFile(path: path);
        }
    }
}
