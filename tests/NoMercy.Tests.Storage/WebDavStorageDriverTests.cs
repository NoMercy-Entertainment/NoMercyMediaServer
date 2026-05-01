using System.Net;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Factory;
using NoMercy.Storage.Remote;
using WebDav;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests — no Docker needed
// ============================================================================

public class WebDavDriverConfigParsingTests
{
    [Fact]
    public void Parse_missing_url_throws()
    {
        Action act = () => WebDavDriverConfig.Parse("""{"username":"user"}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*url*");
    }

    [Fact]
    public void Parse_empty_url_throws()
    {
        Action act = () => WebDavDriverConfig.Parse("""{"url":"  "}""", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*url*");
    }

    [Fact]
    public void Parse_null_config_throws()
    {
        Action act = () => WebDavDriverConfig.Parse(null!, Ulid.NewUlid());
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_malformed_json_throws_ArgumentException()
    {
        Action act = () => WebDavDriverConfig.Parse("{bad json{{", Ulid.NewUlid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_both_basic_and_bearer_throws()
    {
        string json =
            """{"url":"http://dav.example.com/","username":"u","passwordRef":"p","bearerTokenRef":"b"}""";
        Action act = () => WebDavDriverConfig.Parse(json, Ulid.NewUlid());
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*Basic Auth*")
            .And.Message.Should()
            .Contain("Bearer");
    }

    [Fact]
    public void Parse_invalid_timeout_throws()
    {
        string json = """{"url":"http://dav.example.com/","timeoutSeconds":0}""";
        Action act = () => WebDavDriverConfig.Parse(json, Ulid.NewUlid());
        act.Should().Throw<ArgumentException>().WithMessage("*timeoutSeconds*");
    }

    [Fact]
    public void Parse_minimal_config_uses_defaults()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"http://dav.example.com/files/"}""",
            Ulid.NewUlid()
        );

        config.Url.Should().Be("http://dav.example.com/files/");
        config.Username.Should().BeNull();
        config.PasswordRef.Should().BeNull();
        config.BearerTokenRef.Should().BeNull();
        config.IgnoreCertErrors.Should().BeFalse();
        config.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Parse_url_without_trailing_slash_is_normalized()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"http://dav.example.com/files"}""",
            Ulid.NewUlid()
        );

        config.Url.Should().EndWith("/");
    }

    [Fact]
    public void Parse_basic_auth_fields_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"http://dav.example.com/","username":"alice","passwordRef":"vault/alice"}""",
            Ulid.NewUlid()
        );

        config.Username.Should().Be("alice");
        config.PasswordRef.Should().Be("vault/alice");
        config.BearerTokenRef.Should().BeNull();
    }

    [Fact]
    public void Parse_bearer_auth_field_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"http://dav.example.com/","bearerTokenRef":"tokens/mytoken"}""",
            Ulid.NewUlid()
        );

        config.BearerTokenRef.Should().Be("tokens/mytoken");
        config.Username.Should().BeNull();
    }

    [Fact]
    public void Parse_ignoreCertErrors_defaults_to_false()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"https://self-signed.example.com/"}""",
            Ulid.NewUlid()
        );

        config.IgnoreCertErrors.Should().BeFalse();
    }

    [Fact]
    public void Parse_ignoreCertErrors_true_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"https://self-signed.example.com/","ignoreCertErrors":true}""",
            Ulid.NewUlid()
        );

        config.IgnoreCertErrors.Should().BeTrue();
    }

    [Fact]
    public void Parse_custom_timeout_accepted()
    {
        WebDavDriverConfig config = WebDavDriverConfig.Parse(
            """{"url":"http://dav.example.com/","timeoutSeconds":60}""",
            Ulid.NewUlid()
        );

        config.TimeoutSeconds.Should().Be(60);
    }
}

public class WebDavStorageDriverFactoryTests
{
    private static StorageFactory Factory() =>
        new(new LocalStorageDriver(), NullLogger<StorageFactory>.Instance);

    [Fact]
    public void For_webdav_without_config_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        Action act = () => factory.For(Ulid.NewUlid(), "webdav", null, "/irrelevant");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_missing_url_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        string json = """{"username":"user"}""";
        Action act = () => factory.For(Ulid.NewUlid(), "webdav", json, "/irrelevant");
        act.Should().Throw<ArgumentException>().WithMessage("*url*");
    }

    [Fact]
    public void For_webdav_both_auth_schemes_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        string json =
            """{"url":"http://dav.example.com/","username":"u","passwordRef":"p","bearerTokenRef":"b"}""";
        Action act = () => factory.For(Ulid.NewUlid(), "webdav", json, "/irrelevant");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void For_webdav_valid_config_returns_RemoteStorage()
    {
        StorageFactory factory = Factory();
        string json = """{"url":"http://dav.example.com/files/"}""";

        IStorage storage = factory.For(Ulid.NewUlid(), "webdav", json, "/irrelevant");

        storage.Should().NotBeNull().And.BeOfType<RemoteStorage>();
    }

    [Fact]
    public void For_webdav_malformed_config_throws_ArgumentException()
    {
        StorageFactory factory = Factory();
        Action act = () => factory.For(Ulid.NewUlid(), "webdav", "{{{bad json", "/irrelevant");
        act.Should().Throw<ArgumentException>();
    }
}

// ============================================================================
// Integration tests — require Docker + WebDAV container
// ============================================================================

/// <summary>
/// Starts a bytemark/webdav container once per test class.
/// Tests are skipped when Docker is not reachable.
/// </summary>
public sealed class WebDavFixture : IAsyncLifetime
{
    private IContainer? _container;
    public bool Available { get; private set; }
    public string? BaseUrl { get; private set; }

    public const string Username = "testuser";
    public const string Password = "testpass";

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
                .WithImage("bytemark/webdav:latest")
                .WithPortBinding(80, assignRandomHostPort: true)
                .WithEnvironment("AUTH_TYPE", "Basic")
                .WithEnvironment("USERNAME", Username)
                .WithEnvironment("PASSWORD", Password)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(80))
                .Build();

            await _container.StartAsync();

            int port = _container.GetMappedPublicPort(80);
            // bytemark/webdav serves at /
            BaseUrl = $"http://localhost:{port}/";
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
            await _container.DisposeAsync();
    }

    public WebDavStorageDriver BuildDriver()
    {
        WebDavDriverConfig config = WebDavDriverConfig.For(
            BaseUrl!,
            username: Username,
            passwordRef: null,
            ignoreCertErrors: false,
            timeoutSeconds: 30
        );

        // Resolve password directly — no credential store in tests.
        WebDavClient client = new(
            new WebDavClientParams
            {
                BaseAddress = new Uri(BaseUrl!),
                Credentials = new NetworkCredential(Username, Password),
            }
        );

        return new WebDavStorageDriver(client, BaseUrl!);
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

[Collection("WebDavIntegration")]
public class WebDavStorageDriverIntegrationTests(WebDavFixture webDav)
    : IClassFixture<WebDavFixture>
{
    private const string SkipReason = "Docker not available";

    [SkippableFact]
    public async Task RoundTrip_write_read_delete()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string path = $"roundtrip-{Ulid.NewUlid()}.txt";
        byte[] data = "hello webdav"u8.ToArray();

        await using (Stream w = driver.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        using Stream r = driver.OpenRead(path);
        using MemoryStream ms = new();
        await r.CopyToAsync(ms);
        ms.ToArray().Should().Equal(data);

        driver.DeleteFile(path);
        driver.FileExists(path).Should().BeFalse();
    }

    [SkippableFact]
    public async Task LargeFile_write_read()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string path = $"large-{Ulid.NewUlid()}.bin";

        byte[] data = new byte[12 * 1024 * 1024];
        new Random(99).NextBytes(data);

        await using (Stream w = driver.OpenWrite(path, overwrite: true))
            await w.WriteAsync(data);

        long size = driver.GetFileSize(path);
        size.Should().Be(data.Length);

        driver.DeleteFile(path);
    }

    [SkippableFact]
    public async Task Mkcol_recursive_creation()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string dir = $"a-{Ulid.NewUlid()}/b/c";

        driver.CreateDirectory(dir);
        driver.DirectoryExists(dir).Should().BeTrue();

        driver.DeleteDirectory($"a-{dir.Split('/')[0]}", recursive: true);
    }

    [SkippableFact]
    public async Task Propfind_enumerate_with_pattern()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string dirName = $"enum-{Ulid.NewUlid()}";
        driver.CreateDirectory(dirName);

        string fileA = $"{dirName}/a.txt";
        string fileB = $"{dirName}/b.txt";
        string fileC = $"{dirName}/c.bin";
        byte[] bytes = "x"u8.ToArray();

        await using (Stream w = driver.OpenWrite(fileA, overwrite: true))
            await w.WriteAsync(bytes);
        await using (Stream w2 = driver.OpenWrite(fileB, overwrite: true))
            await w2.WriteAsync(bytes);
        await using (Stream w3 = driver.OpenWrite(fileC, overwrite: true))
            await w3.WriteAsync(bytes);

        IEnumerable<string> entries = driver.EnumerateFileSystemEntries(
            dirName,
            "*.txt",
            SearchOption.TopDirectoryOnly
        );

        entries.Should().HaveCount(2);

        driver.DeleteDirectory(dirName, recursive: true);
    }

    [SkippableFact]
    public async Task MoveFile_renames_resource()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string src = $"move-src-{Ulid.NewUlid()}.txt";
        string dst = $"move-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "move me"u8.ToArray();

        await using (Stream w = driver.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        driver.MoveFile(src, dst);

        driver.FileExists(src).Should().BeFalse();
        driver.FileExists(dst).Should().BeTrue();

        driver.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task CopyFile_duplicates_resource()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavStorageDriver driver = webDav.BuildDriver();
        string src = $"copy-src-{Ulid.NewUlid()}.txt";
        string dst = $"copy-dst-{Ulid.NewUlid()}.txt";
        byte[] data = "copy me"u8.ToArray();

        await using (Stream w = driver.OpenWrite(src, overwrite: true))
            await w.WriteAsync(data);

        driver.CopyFile(src, dst, overwrite: true);

        driver.FileExists(src).Should().BeTrue();
        driver.FileExists(dst).Should().BeTrue();

        driver.DeleteFile(src);
        driver.DeleteFile(dst);
    }

    [SkippableFact]
    public async Task BasicAuth_wrong_password_fails()
    {
        Skip.If(!webDav.Available, SkipReason);

        WebDavClient badClient = new(
            new WebDavClientParams
            {
                BaseAddress = new Uri(webDav.BaseUrl!),
                Credentials = new NetworkCredential("testuser", "wrongpassword"),
            }
        );
        WebDavStorageDriver driver = new(badClient, webDav.BaseUrl!);

        // Propfind on root with wrong creds should return an HTTP error (401/403).
        Action act = () => driver.DirectoryExists("/");
        // Some servers return 401 as non-successful; the driver returns false (not exception)
        // because FileExists/DirectoryExists swallow non-success responses.
        bool result = driver.DirectoryExists("/");
        result.Should().BeFalse("401/403 responses should be treated as 'not found'");
    }
}

[CollectionDefinition("WebDavIntegration")]
public class WebDavIntegrationCollection : ICollectionFixture<WebDavFixture> { }
