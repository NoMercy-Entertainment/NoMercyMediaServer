using System.Net;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Remote;
using WebDav;

namespace NoMercy.Tests.Storage.Contract;

// ---------------------------------------------------------------------------
// bytemark/webdav class-level fixture.
//
// A single WebDAV container starts once for all tests in this class.
// The raw HttpClient used for seed/verify is kept on the fixture so it lives
// across tests without being re-created per call.
// ---------------------------------------------------------------------------

public sealed class WebDavContractFixture : IAsyncLifetime
{
    private IContainer? _container;

    public bool Available { get; private set; }
    public string BaseUrl { get; private set; } = string.Empty;

    public const string Username = "testuser";
    public const string Password = "testpass";

    // Shared raw HTTP client — used by SeedFile, SeedDirectory, BackendHasFile.
    public HttpClient RawHttp { get; private set; } = new();

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
            BaseUrl = $"http://localhost:{port}/";
            Available = true;

            HttpClientHandler handler = new()
            {
                Credentials = new NetworkCredential(Username, Password),
                PreAuthenticate = true,
            };
            RawHttp = new HttpClient(handler, disposeHandler: true);
        }
        catch
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        RawHttp.Dispose();

        if (_container is not null)
            await _container.DisposeAsync();
    }

    public WebDavStorageDriver BuildDriver()
    {
        // Hard-set the Authorization header on the HttpClient so every
        // request carries Basic auth — bytemark/webdav rejects PROPFIND on
        // sub-collections without an Authorization header (challenge-response
        // doesn't seem to fire from the WebDav.Client library for those
        // requests). HttpClientHandler.Credentials only triggers on a 401
        // challenge round-trip; PreAuthenticate doesn't help across distinct
        // paths the way operators expect.
        string token = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{Username}:{Password}")
        );
        HttpClient httpClient = new() { BaseAddress = new Uri(BaseUrl) };
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", token);
        WebDavClient client = new(httpClient);

        return new WebDavStorageDriver(client, BaseUrl);
    }

    private static async Task<bool> DockerAvailableAsync()
    {
        try
        {
            using HttpClient http = new();
            http.Timeout = TimeSpan.FromSeconds(3);
            HttpResponseMessage response = await http.GetAsync("http://localhost:2375/info");
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

// Required by xUnit so the fixture is shared across the collection.
[CollectionDefinition("WebDavContractIntegration")]
public sealed class WebDavContractIntegrationCollection
    : ICollectionFixture<WebDavContractFixture> { }

/// <summary>
/// Runs the shared IStorage contract suite against a real bytemark/webdav container.
///
/// Design:
///   - <see cref="WebDavContractFixture"/> starts one container for the whole class.
///   - Each test in the base class calls <see cref="CreateStorage"/> then
///     <see cref="DisposeStorage"/> in its own try/finally.
///   - <see cref="CreateStorage"/> calls <c>Skip.If(!_fixture.Available)</c> so every
///     inherited [Fact] skips cleanly when Docker is absent.
///   - Seed helpers use raw HTTP (MKCOL / PUT) and PROPFIND to verify, bypassing
///     the driver under test.
/// </summary>
[Collection("WebDavContractIntegration")]
[Trait("Category", "Integration")]
public sealed class WebDavStorageContractTests(WebDavContractFixture fixture)
    : IStorageContractTests
{
    // -----------------------------------------------------------------------
    // IStorageContractTests hooks
    // -----------------------------------------------------------------------

    protected override IStorage CreateStorage()
    {
        Skip.If(
            !fixture.Available,
            "Docker / WebDAV not available — skipping WebDAV contract test"
        );

        return new RemoteStorage(fixture.BuildDriver());
    }

    /// <summary>
    /// Seed bypasses the driver — PUT directly via the raw HttpClient.
    /// </summary>
    protected override async Task SeedFile(string relativePath, byte[] content)
    {
        // Ensure parent directories exist.
        string normalized = relativePath.TrimStart('/');
        string? parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent))
            await SeedDirectory(parent);

        string url = fixture.BaseUrl + normalized;
        using ByteArrayContent body = new(content);
        HttpResponseMessage response = await fixture.RawHttp.PutAsync(url, body);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// WebDAV has real directory objects — MKCOL each ancestor in order.
    /// 405 MethodNotAllowed means the collection already exists; that's fine.
    /// </summary>
    protected override async Task SeedDirectory(string relativePath)
    {
        string normalized = relativePath.TrimStart('/').TrimEnd('/');
        string[] segments = normalized.Split('/');
        string accumulated = string.Empty;

        foreach (string segment in segments)
        {
            accumulated = string.IsNullOrEmpty(accumulated) ? segment : accumulated + "/" + segment;

            string url = fixture.BaseUrl + accumulated + "/";
            using HttpRequestMessage mkcol = new(new HttpMethod("MKCOL"), url);
            HttpResponseMessage response = await fixture.RawHttp.SendAsync(mkcol);

            if (
                !response.IsSuccessStatusCode
                && response.StatusCode != HttpStatusCode.MethodNotAllowed
            )
                response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Verify presence via PROPFIND Depth: 0, bypassing the driver.
    /// Returns true only for HTTP 207 Multi-Status responses.
    /// </summary>
    protected override async Task<bool> BackendHasFile(string relativePath)
    {
        string url = fixture.BaseUrl + relativePath.TrimStart('/');
        using HttpRequestMessage propfind = new(new HttpMethod("PROPFIND"), url);
        propfind.Headers.Add("Depth", "0");
        HttpResponseMessage response = await fixture.RawHttp.SendAsync(propfind);
        return response.StatusCode == HttpStatusCode.MultiStatus;
    }

    protected override async Task DisposeStorage()
    {
        // Per-test isolation — wipe everything in the WebDAV server's root
        // before the next test runs. The container persists across the test
        // class (per-class fixture) so without this each test sees leftover
        // files from earlier tests, breaking "list shows exactly N entries"
        // assertions in unpredictable ways.
        if (!fixture.Available)
            return;

        try
        {
            // Enumerate top-level resources and DELETE each. Recursive deletes
            // on collections take the whole subtree.
            using HttpRequestMessage propfind = new(new HttpMethod("PROPFIND"), fixture.BaseUrl);
            propfind.Headers.Add("Depth", "1");
            HttpResponseMessage list = await fixture.RawHttp.SendAsync(propfind);
            if (list.StatusCode != HttpStatusCode.MultiStatus)
                return;

            string body = await list.Content.ReadAsStringAsync();
            // Crude href extraction — good enough for this fixture.
            System.Text.RegularExpressions.MatchCollection matches =
                System.Text.RegularExpressions.Regex.Matches(
                    body,
                    "<D:href>([^<]+)</D:href>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                string href = m.Groups[1].Value;
                // Skip the root itself.
                Uri full = new(new Uri(fixture.BaseUrl), href);
                if (full.AbsoluteUri.TrimEnd('/') == fixture.BaseUrl.TrimEnd('/'))
                    continue;

                using HttpRequestMessage del = new(HttpMethod.Delete, full);
                _ = await fixture.RawHttp.SendAsync(del);
            }
        }
        catch
        {
            // Best effort — cleanup failures shouldn't fail the test.
        }
    }

    // -----------------------------------------------------------------------
    // WebDAV-specific overrides — document known driver divergences
    // -----------------------------------------------------------------------

    // ExistsAsync for root ("") — bytemark/webdav responds to PROPFIND on "/"
    // with 207. Expected to pass.

    // Backslash normalisation — WebDavStorageDriver replaces '\' with '/' before
    // building the URL. Expected to pass.

    // Double-slash normalisation — WebDavStorageDriver does NOT collapse "//".
    // "foo//bar.bin" and "foo/bar.bin" are different URLs on WebDAV.
    // This test is expected to FAIL until the driver normalises paths.
    // Named distinctly to avoid xUnit1024 (test method name conflict with base class).
    [Fact]
    [Trait("Category", "Integration")]
    public async Task WebDav_double_slash_is_known_failure_requires_driver_normalisation()
    {
        Skip.If(!fixture.Available, "Docker not available");

        IStorage storage = CreateStorage();
        try
        {
            byte[] data = new byte[] { 0x01, 0x02 };
            await SeedFile("foo/bar.bin", data);

            bool withSingle = await storage.ExistsAsync("foo/bar.bin", CancellationToken.None);

            // KNOWN FAILURE: WebDavStorageDriver does not collapse double slashes.
            // "foo//bar.bin" is a different WebDAV URL from "foo/bar.bin".
            // Driver fix needed: normalise consecutive slashes before URL construction.
            bool withDouble = await storage.ExistsAsync("foo//bar.bin", CancellationToken.None);

            withSingle.Should().BeTrue();
            withDouble
                .Should()
                .Be(
                    withSingle,
                    "KNOWN FAILURE: WebDavStorageDriver does not collapse double slashes — driver fix needed"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // Absolute path rejection — RemoteStorage.V() calls StructuralValidate
    // which throws StoragePathNotAllowedException for leading-slash, drive-letter,
    // and UNC paths before the WebDAV driver builds a URL.

    // Null-byte in path — StructuralValidate throws StoragePathNotAllowedException
    // before the driver is reached, satisfying the base contract's "throws" assertion.

    // ".." traversal — StructuralValidate throws StoragePathNotAllowedException
    // before the driver builds any URL.
}
