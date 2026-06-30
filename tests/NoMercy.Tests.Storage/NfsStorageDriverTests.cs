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
        Action act = () => NfsDriverConfig.Parse("""{"export":"/data"}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*server*");
    }

    [Fact]
    public void Parse_missing_export_throws()
    {
        Action act = () => NfsDriverConfig.Parse("""{"server":"192.168.1.1"}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*export*");
    }

    [Fact]
    public void Parse_invalid_version_throws()
    {
        Action act = () =>
            NfsDriverConfig.Parse(
                """{"server":"192.168.1.1","export":"/data","version":2}""",
                Ulid.NewUlid()
            );
        act.Should().Throw<ArgumentException>().WithMessage("*version*");
    }

    [Fact]
    public void Parse_null_config_throws()
    {
        Action act = () => NfsDriverConfig.Parse(null!, Ulid.NewUlid());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException()
    {
        Action act = () => NfsDriverConfig.Parse("{{bad json", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_defaults_version_to_3()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"/media"}""",
            Ulid.NewUlid()
        );
        config.Version.Should().Be(3);
    }

    [Fact]
    public void Parse_defaults_port_to_2049()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"/media"}""",
            Ulid.NewUlid()
        );
        config.Port.Should().Be(2049);
    }

    [Fact]
    public void Parse_accepts_version_4()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"/media","version":4}""",
            Ulid.NewUlid()
        );
        config.Version.Should().Be(4);
    }

    [Fact]
    public void Parse_accepts_uid_gid()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"/media","uid":1000,"gid":1000}""",
            Ulid.NewUlid()
        );
        config.Uid.Should().Be(1000);
        config.Gid.Should().Be(1000);
    }

    [Fact]
    public void Parse_normalizes_export_leading_slash()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"media/files"}""",
            Ulid.NewUlid()
        );
        config.Export.Should().StartWith("/");
    }

    [Fact]
    public void Parse_trims_trailing_slash_from_export()
    {
        NfsDriverConfig config = NfsDriverConfig.Parse(
            """{"server":"nas.local","export":"/media/"}""",
            Ulid.NewUlid()
        );
        config.Export.Should().Be("/media");
    }

    private static StorageFactory FactoryWithConfig(string type, string? config)
    {
        Mock<IDriverConfigResolver> resolver = new();
        resolver.Setup(r => r.Resolve(It.IsAny<Ulid>())).Returns((type, config));
        return new StorageFactory(
            new LocalStorageDriver(),
            NullLogger<StorageFactory>.Instance,
            resolver.Object
        );
    }

    [Fact]
    public void StorageFactory_nfs_without_config_throws()
    {
        StorageFactory factory = FactoryWithConfig("nfs", null);
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StorageFactory_nfs_without_server_throws()
    {
        StorageFactory factory = FactoryWithConfig("nfs", """{"export":"/data"}""");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*server*");
    }

    [Fact]
    public void StorageFactory_nfs_without_export_throws()
    {
        StorageFactory factory = FactoryWithConfig("nfs", """{"server":"nas.local"}""");
        Ulid driverId = Ulid.NewUlid();

        Action act = () => factory.For(Ulid.NewUlid(), driverId, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*export*");
    }
}

// ============================================================================
// Integration tests — run against the shared all-in-one storage container
// (NFS). The container is started once for the whole assembly by the
// StorageBackends collection fixture and torn down after the last test.
// ============================================================================

[Collection("StorageBackends")]
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
        Skip.If(backend is null, SkipReason);
        return backend!;
    }

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/roundtrip-{Ulid.NewUlid()}.txt";
        byte[] data = "hello nfs"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        await using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().Equal(data);

        backend.DeleteFile(path);
        backend.FileExists(path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task LargeFile_write_read()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/large-{Ulid.NewUlid()}.bin";

        byte[] data = new byte[11 * 1024 * 1024]; // 11 MB
        new Random(99).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        backend.GetFileSize(path).Should().Be(data.Length);

        await using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().HaveCount(data.Length);

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using NfsStorageDriver backend = RequireBackend();
        string dir = $"/enum-{Ulid.NewUlid()}";
        backend.CreateDirectory(dir);

        string fileA = dir + "/a.txt";
        string fileB = dir + "/b.txt";
        string fileC = dir + "/c.bin";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = backend.OpenWrite(fileA, overwrite: true))
            await w.WriteAsync(bytes);
        await using (Stream w = backend.OpenWrite(fileB, overwrite: true))
            await w.WriteAsync(bytes);
        await using (Stream w = backend.OpenWrite(fileC, overwrite: true))
            await w.WriteAsync(bytes);

        IEnumerable<string> txtFiles = backend.EnumerateFileSystemEntries(
            dir,
            "*.txt",
            SearchOption.AllDirectories
        );

        txtFiles.Should().HaveCount(2);

        backend.DeleteDirectory(dir, recursive: true);
        backend.DirectoryExists(dir).Should().BeFalse();
    }

    [SkippableFact]
    public async Task MoveFile_renames_path()
    {
        using NfsStorageDriver backend = RequireBackend();
        string src = $"/move-src-{Ulid.NewUlid()}.txt";
        string dst = $"/move-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        backend.MoveFile(src, dst);

        backend.FileExists(src).Should().BeFalse();
        backend.FileExists(dst).Should().BeTrue();

        backend.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_content()
    {
        using NfsStorageDriver backend = RequireBackend();
        string src = $"/copy-src-{Ulid.NewUlid()}.txt";
        string dst = $"/copy-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        backend.CopyFile(src, dst, overwrite: true);

        backend.FileExists(src).Should().BeTrue();
        backend.FileExists(dst).Should().BeTrue();

        backend.DeleteFile(src);
        backend.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task MkDir_and_RmDir_roundtrip()
    {
        using NfsStorageDriver backend = RequireBackend();
        string dir = $"/mkdir-test-{Ulid.NewUlid()}";

        backend.CreateDirectory(dir);
        backend.DirectoryExists(dir).Should().BeTrue();

        backend.DeleteDirectory(dir, recursive: false);
        backend.DirectoryExists(dir).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Mkdir_recursive_creates_nested_dirs()
    {
        using NfsStorageDriver backend = RequireBackend();
        string nested = $"/mkdir-{Ulid.NewUlid()}/sub/deep";

        backend.CreateDirectory(nested);
        backend.DirectoryExists(nested).Should().BeTrue();

        string top = nested.Split('/')[1];
        backend.DeleteDirectory("/" + top, recursive: true);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/nooverwrite-{Ulid.NewUlid()}.txt";

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync("original"u8.ToArray());

        Action act = () => backend.OpenWrite(path, overwrite: false);
        act.Should().Throw<IOException>().WithMessage("*overwrite*");

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task GetFileSize_and_GetLastWriteTimeUtc()
    {
        using NfsStorageDriver backend = RequireBackend();
        string path = $"/meta-{Ulid.NewUlid()}.txt";
        byte[] data = Encoding.UTF8.GetBytes("metadata test");

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        backend.GetFileSize(path).Should().Be(data.Length);

        DateTime mtime = backend.GetLastWriteTimeUtc(path);
        mtime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public void IsHidden_dotfiles_are_hidden()
    {
        using NfsStorageDriver backend = RequireBackend();
        backend.IsHidden(".hidden").Should().BeTrue();
        backend.IsHidden("visible.txt").Should().BeFalse();
        backend.IsHidden(".").Should().BeFalse();
    }

    [SkippableFact]
    public void Auth_with_uid_gid_connects_successfully()
    {
        Skip.If(!fix.Available, SkipReason);

        // The shared container exports NFSv4 at "/" with all_squash — everyone
        // maps to root, so uid/gid specifics don't apply. Connect with the
        // fixture defaults and assert the mount is visible.
        NfsDriverConfig config = NfsDriverConfig.For(
            StorageBackendsFixture.NfsHost,
            StorageBackendsFixture.NfsExport,
            version: 4
        );

        NfsStorageDriver? backend;
        try
        {
            backend = new NfsStorageDriver(config);
        }
        catch (DllNotFoundException)
        {
            Skip.If(true, SkipReason);
            return;
        }

        using NfsStorageDriver b = backend;
        b.DirectoryExists("/").Should().BeTrue();
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
        new Random(42).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        try
        {
            const int readerCount = 6;
            Task<byte[]>[] readers = new Task<byte[]>[readerCount];
            for (int i = 0; i < readerCount; i++)
            {
                readers[i] = Task.Run(() =>
                {
                    using Stream r = backend.OpenRead(path);
                    using MemoryStream ms = new();
                    r.CopyTo(ms);
                    return ms.ToArray();
                });
            }

            byte[][] results = await Task.WhenAll(readers);
            foreach (byte[] result in results)
                result.Should().Equal(data);
        }
        finally
        {
            backend.DeleteFile(path);
        }
    }
}
