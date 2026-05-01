using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class S3BackendConfigParsingTests
{
    [Fact]
    public void S3BackendConfig_missing_bucket_throws()
    {
        string json = """{"region":"us-east-1"}""";

        // The factory validates bucket before constructing backend
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "s3", json, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*bucket*");
    }

    [Fact]
    public void S3BackendConfig_missing_region_throws()
    {
        string json = """{"bucket":"mybucket"}""";

        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "s3", json, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*region*");
    }

    [Fact]
    public void R2_without_endpoint_throws()
    {
        string json = """{"bucket":"mybucket","region":"auto"}""";

        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "r2", json, "/irrelevant");

        act.Should().Throw<ArgumentException>().WithMessage("*endpoint*");
    }

    [Fact]
    public void Null_configJson_for_s3_throws()
    {
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "s3", null, "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Malformed_configJson_for_s3_throws_ArgumentException()
    {
        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "s3", "not-json{{{", "/irrelevant");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void S3_with_valid_config_returns_RemoteStorage()
    {
        // This test does NOT need Docker — it just validates the factory
        // builds a RemoteStorage instance. The backend will fail at actual
        // S3 call time, not at construction time.
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";

        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        IStorage storage = factory.For(Ulid.NewUlid(), "s3", json, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void R2_with_endpoint_returns_RemoteStorage()
    {
        string json =
            """{"bucket":"test","region":"auto","endpoint":"https://account.r2.cloudflarestorage.com"}""";

        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        IStorage storage = factory.For(Ulid.NewUlid(), "r2", json, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    // The old test expected NotSupportedException — now expects a working backend.
    [Fact]
    public void For_s3_r2_no_longer_throws_NotSupportedException()
    {
        string json =
            """{"bucket":"test","region":"us-east-1","endpoint":"http://localhost:9000"}""";

        StorageFactory factory = new(
            new SystemIoStorageBackend(),
            NullLogger<StorageFactory>.Instance
        );

        Action act = () => factory.For(Ulid.NewUlid(), "s3", json, "/irrelevant");

        act.Should().NotThrow<NotSupportedException>();
    }
}

// ============================================================================
// Integration tests — require Docker + MinIO
// ============================================================================

/// <summary>
/// Starts a MinIO container once per test class (class fixture).
/// Tests are skipped when Docker is not reachable.
/// </summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private IContainer? _container;
    public bool Available { get; private set; }
    public string? Endpoint { get; private set; }
    public const string AccessKey = "minioadmin";
    public const string SecretKey = "minioadmin";
    public const string BucketName = "nomercy-test";

    public async Task InitializeAsync()
    {
        if (!await DockerAvailableAsync())
        {
            Available = false;
            return;
        }

        try
        {
            _container = new ContainerBuilder()
                .WithImage("minio/minio:latest")
                .WithPortBinding(9000, assignRandomHostPort: true)
                .WithEnvironment("MINIO_ROOT_USER", AccessKey)
                .WithEnvironment("MINIO_ROOT_PASSWORD", SecretKey)
                .WithCommand("server", "/data")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9000))
                .Build();

            await _container.StartAsync();

            int port = _container.GetMappedPublicPort(9000);
            Endpoint = $"http://localhost:{port}";
            Available = true;

            // Create the test bucket
            using AmazonS3Client client = BuildClient();
            await client.PutBucketAsync(BucketName);
        }
        catch (Exception)
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    public AmazonS3Client BuildClient()
    {
        AmazonS3Config cfg = new() { ServiceURL = Endpoint, ForcePathStyle = true };
        return new AmazonS3Client(
            new Amazon.Runtime.BasicAWSCredentials(AccessKey, SecretKey),
            cfg
        );
    }

    public S3StorageBackend BuildBackend(string? prefix = null)
    {
        AmazonS3Client client = BuildClient();
        return new S3StorageBackend(client, BucketName, prefix);
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
            // Also try named pipe / socket probe via Docker CLI
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

[Collection("MinioIntegration")]
public class S3StorageBackendIntegrationTests(MinioFixture minio) : IClassFixture<MinioFixture>
{
    private const string SkipReason = "Docker not available";

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string path = $"roundtrip/{Ulid.NewUlid()}.txt";
        byte[] data = "hello s3"u8.ToArray();

        // Write
        await using (Stream w = backend.OpenWrite(path, overwrite: true))
        {
            await w.WriteAsync(data);
        }

        // Read
        using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().Equal(data);

        // Delete
        backend.DeleteFile(path);
        backend.FileExists(path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Multipart_upload_large_file()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string path = $"large/{Ulid.NewUlid()}.bin";

        // 6 MB — exceeds the 5 MB multipart threshold
        byte[] data = new byte[6 * 1024 * 1024];
        new Random(42).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
        {
            await w.WriteAsync(data);
        }

        long size = backend.GetFileSize(path);
        size.Should().Be(data.Length);

        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task EnumerateFileSystemEntries_with_prefix()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend("enum-test");
        string prefix = $"dir-{Ulid.NewUlid()}";

        // Write two files under the prefix
        string fileA = $"{prefix}/a.txt";
        string fileB = $"{prefix}/b.txt";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = backend.OpenWrite(fileA, overwrite: true))
            await w.WriteAsync(bytes);
        await using (Stream w2 = backend.OpenWrite(fileB, overwrite: true))
            await w2.WriteAsync(bytes);

        IEnumerable<string> entries = backend.EnumerateFileSystemEntries(
            prefix,
            "*.txt",
            SearchOption.AllDirectories
        );

        entries.Should().HaveCount(2);

        backend.DeleteDirectory(prefix, recursive: true);
    }

    [SkippableFact]
    public async Task MoveFile_renames_key()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string src = $"move/{Ulid.NewUlid()}-src.txt";
        string dst = $"move/{Ulid.NewUlid()}-dst.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = backend.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        backend.MoveFile(src, dst);

        backend.FileExists(src).Should().BeFalse();
        backend.FileExists(dst).Should().BeTrue();

        backend.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_key()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string src = $"copy/{Ulid.NewUlid()}-src.txt";
        string dst = $"copy/{Ulid.NewUlid()}-dst.txt";
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
    public async Task OpenRead_large_file_streams_correctly()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string path = $"stream/{Ulid.NewUlid()}.bin";
        byte[] data = new byte[2 * 1024 * 1024];
        new Random(7).NextBytes(data);

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        using Stream r = backend.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);

        ms.ToArray().Should().HaveCount(data.Length);
        backend.DeleteFile(path);
    }

    [SkippableFact]
    public async Task OpenWrite_overwrite_false_rejects_existing()
    {
        Skip.If(!minio.Available, SkipReason);

        S3StorageBackend backend = minio.BuildBackend();
        string path = $"nooverwrite/{Ulid.NewUlid()}.txt";
        byte[] data = "original"u8.ToArray();

        await using (Stream w = backend.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        Action act = () => backend.OpenWrite(path, overwrite: false);
        act.Should().Throw<IOException>().WithMessage("*overwrite*");

        backend.DeleteFile(path);
    }
}

// Required by xUnit to share the fixture across the collection
[CollectionDefinition("MinioIntegration")]
public class MinioIntegrationCollection : ICollectionFixture<MinioFixture> { }
