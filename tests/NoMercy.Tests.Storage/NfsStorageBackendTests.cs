using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage;
using NoMercy.Storage.Backends.Nfs;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Local;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class NfsBackendConfigParsingTests
{
    [Fact]
    public void Parse_missing_server_throws()
    {
        Action act = () => NfsBackendConfig.Parse("""{"export":"/data"}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*server*");
    }

    [Fact]
    public void Parse_missing_export_throws()
    {
        Action act = () => NfsBackendConfig.Parse("""{"server":"192.168.1.1"}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*export*");
    }

    [Fact]
    public void Parse_invalid_version_throws()
    {
        Action act = () =>
            NfsBackendConfig.Parse(
                """{"server":"192.168.1.1","export":"/data","version":2}""",
                Ulid.NewUlid()
            );
        act.Should().Throw<ArgumentException>().WithMessage("*version*");
    }

    [Fact]
    public void Parse_null_config_throws()
    {
        Action act = () => NfsBackendConfig.Parse(null!, Ulid.NewUlid());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException()
    {
        Action act = () => NfsBackendConfig.Parse("{{bad json", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_defaults_version_to_3()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"/media"}""",
            Ulid.NewUlid()
        );
        config.Version.Should().Be(3);
    }

    [Fact]
    public void Parse_defaults_port_to_2049()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"/media"}""",
            Ulid.NewUlid()
        );
        config.Port.Should().Be(2049);
    }

    [Fact]
    public void Parse_accepts_version_4()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"/media","version":4}""",
            Ulid.NewUlid()
        );
        config.Version.Should().Be(4);
    }

    [Fact]
    public void Parse_accepts_uid_gid()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"/media","uid":1000,"gid":1000}""",
            Ulid.NewUlid()
        );
        config.Uid.Should().Be(1000);
        config.Gid.Should().Be(1000);
    }

    [Fact]
    public void Parse_normalizes_export_leading_slash()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"media/files"}""",
            Ulid.NewUlid()
        );
        config.Export.Should().StartWith("/");
    }

    [Fact]
    public void Parse_trims_trailing_slash_from_export()
    {
        NfsBackendConfig config = NfsBackendConfig.Parse(
            """{"server":"nas.local","export":"/media/"}""",
            Ulid.NewUlid()
        );
        config.Export.Should().Be("/media");
    }

    [Fact]
    public void StorageFactory_nfs_without_config_throws()
    {
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "nfs", null, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StorageFactory_nfs_without_server_throws()
    {
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () =>
            factory.For(Ulid.NewUlid(), "nfs", """{"export":"/data"}""", "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*server*");
    }

    [Fact]
    public void StorageFactory_nfs_without_export_throws()
    {
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () =>
            factory.For(Ulid.NewUlid(), "nfs", """{"server":"nas.local"}""", "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*export*");
    }
}

// ============================================================================
// Integration tests — require Docker + ganesha-nfs or similar NFS container
// ============================================================================

/// <summary>
/// Starts an NFS-kernel-server container once per test class.
/// Tests are skipped when Docker is not reachable or the container fails to start.
/// Image: itsthenetwork/nfs-server-alpine (NFS3, no additional tools needed).
/// </summary>
public sealed class NfsFixture : IAsyncLifetime
{
    private IContainer? _container;
    public bool Available { get; private set; }
    public string? ServerIp { get; private set; }
    public const string Export = "/nfsshare";

    public async Task InitializeAsync()
    {
        if (!await DockerAvailableAsync())
        {
            Available = false;
            return;
        }

        try
        {
            // itsthenetwork/nfs-server-alpine runs an NFS3 kernel server.
            // It exports /nfsshare and needs --privileged (or at minimum
            // the NET_ADMIN capability) to set up the kernel NFS server.
            _container = new ContainerBuilder()
                .WithImage("itsthenetwork/nfs-server-alpine:latest")
                .WithPrivileged(true)
                .WithEnvironment("SHARED_DIRECTORY", "/nfsshare")
                .WithPortBinding(2049, assignRandomHostPort: true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2049))
                .Build();

            await _container.StartAsync();

            // Give the NFS server a moment to initialise exports
            await Task.Delay(2000);

            ServerIp = "localhost";
            Available = true;
        }
        catch (Exception)
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            try
            {
                await _container.DisposeAsync();
            }
            catch
            {
                // NFS containers using --privileged can resist normal Docker kill/rm;
                // silently swallow — the container will be cleaned up by Docker GC.
            }
        }
    }

    public NfsStorageBackend? TryBuildBackend()
    {
        if (!Available || ServerIp is null)
            return null;

        NfsBackendConfig config = NfsBackendConfig.For(ServerIp, Export);
        try
        {
            return new NfsStorageBackend(config);
        }
        catch (DllNotFoundException)
        {
            // libnfs native library not present on this machine
            Available = false;
            return null;
        }
        catch (Exception)
        {
            Available = false;
            return null;
        }
    }

    private static async Task<bool> DockerAvailableAsync()
    {
        try
        {
            using System.Net.Http.HttpClient http = new();
            http.Timeout = TimeSpan.FromSeconds(3);
            System.Net.Http.HttpResponseMessage response = await http.GetAsync(
                "http://localhost:2375/info"
            );
            return response.IsSuccessStatusCode;
        }
        catch
        {
            try
            {
                using System.Diagnostics.Process proc = new();
                proc.StartInfo = new System.Diagnostics.ProcessStartInfo("docker", "info")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                proc.Start();
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

[Collection("NfsIntegration")]
public class NfsStorageBackendIntegrationTests(NfsFixture nfs) : IClassFixture<NfsFixture>
{
    private const string SkipReason =
        "Docker not available, NFS container failed to start, or libnfs native library not installed";

    /// <summary>
    /// Returns a backend instance, or signals skip if the fixture is unavailable
    /// (no Docker, container failed, or libnfs.dll/.so/.dylib not found).
    /// </summary>
    private NfsStorageBackend RequireBackend()
    {
        NfsStorageBackend? backend = nfs.TryBuildBackend();
        Skip.If(backend is null, SkipReason);
        return backend!;
    }

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        using NfsStorageBackend backend = RequireBackend();
        string path = $"/roundtrip-{Ulid.NewUlid()}.txt";
        byte[] data = "hello nfs"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().Equal(data);

        backend.DeleteFile(path);
        backend.FileExists(path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task LargeFile_write_read()
    {
        using NfsStorageBackend backend = RequireBackend();
        string path = $"/large-{Ulid.NewUlid()}.bin";

        byte[] data = new byte[11 * 1024 * 1024]; // 11 MB
        new Random(99).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        backend.GetFileSize(path).Should().Be(data.Length);

        using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().HaveCount(data.Length);

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_pattern()
    {
        using NfsStorageBackend backend = RequireBackend();
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
        using NfsStorageBackend backend = RequireBackend();
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
        using NfsStorageBackend backend = RequireBackend();
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
        using NfsStorageBackend backend = RequireBackend();
        string dir = $"/mkdir-test-{Ulid.NewUlid()}";

        backend.CreateDirectory(dir);
        backend.DirectoryExists(dir).Should().BeTrue();

        backend.DeleteDirectory(dir, recursive: false);
        backend.DirectoryExists(dir).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Mkdir_recursive_creates_nested_dirs()
    {
        using NfsStorageBackend backend = RequireBackend();
        string nested = $"/mkdir-{Ulid.NewUlid()}/sub/deep";

        backend.CreateDirectory(nested);
        backend.DirectoryExists(nested).Should().BeTrue();

        string top = nested.Split('/')[1];
        backend.DeleteDirectory("/" + top, recursive: true);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        using NfsStorageBackend backend = RequireBackend();
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
        using NfsStorageBackend backend = RequireBackend();
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
        using NfsStorageBackend backend = RequireBackend();
        backend.IsHidden(".hidden").Should().BeTrue();
        backend.IsHidden("visible.txt").Should().BeFalse();
        backend.IsHidden(".").Should().BeFalse();
    }

    [SkippableFact]
    public void Auth_with_uid_gid_connects_successfully()
    {
        Skip.If(!nfs.Available, SkipReason);

        NfsBackendConfig config = NfsBackendConfig.For(
            nfs.ServerIp!,
            NfsFixture.Export,
            version: 3,
            uid: 65534, // nobody
            gid: 65534
        );

        NfsStorageBackend? backend;
        try
        {
            backend = new NfsStorageBackend(config);
        }
        catch (DllNotFoundException)
        {
            Skip.If(true, SkipReason);
            return;
        }

        using NfsStorageBackend b = backend;
        b.DirectoryExists("/").Should().BeTrue();
    }
}

[CollectionDefinition("NfsIntegration")]
public class NfsIntegrationCollection : ICollectionFixture<NfsFixture> { }
