using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginRepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public PluginRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nomercy-repo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static PluginRepositoryManifest CreateTestManifest(string name = "test-repo", int pluginCount = 2)
    {
        List<PluginRepositoryEntry> plugins = [];
        for (int i = 0; i < pluginCount; i++)
        {
            plugins.Add(new()
            {
                Id = Guid.NewGuid(),
                Name = $"Plugin{i}",
                Description = $"Test plugin {i}",
                Author = "Test Author",
                Versions =
                [
                    new()
                    {
                        Version = "1.0.0",
                        DownloadUrl = $"https://example.com/plugin{i}-1.0.0.zip",
                        TargetAbi = "9.0.0",
                        Changelog = "Initial release"
                    }
                ]
            });
        }

        return new()
        {
            Name = name,
            Url = "https://example.com/repo",
            Plugins = plugins
        };
    }

    private static HttpClient CreateMockHttpClient(PluginRepositoryManifest manifest)
    {
        string json = JsonSerializer.Serialize(manifest);
        MockHttpHandler handler = new(json, HttpStatusCode.OK);
        return new(handler);
    }

    private static HttpClient CreateFailingHttpClient()
    {
        MockHttpHandler handler = new("", HttpStatusCode.InternalServerError);
        return new(handler);
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Action act = () => new PluginRepository(null!, NullLogger.Instance, _tempDir);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Action act = () => new PluginRepository(new(), null!, _tempDir);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentException()
    {
        Action act = () => new PluginRepository(new(), NullLogger.Instance, null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_CreatesConfigurationsDirectory()
    {
        string configDir = Path.Combine(_tempDir, "configurations");

        _ = new PluginRepository(new(), NullLogger.Instance, _tempDir);

        Directory.Exists(configDir).Should().BeTrue();
    }

    [Fact]
    public void GetRepositories_Empty_ReturnsEmptyList()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        IReadOnlyList<PluginRepositoryInfo> repos = repo.GetRepositories();

        repos.Should().BeEmpty();
    }

    [Fact]
    public async Task AddRepositoryAsync_AddsRepository()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");

        IReadOnlyList<PluginRepositoryInfo> repos = repo.GetRepositories();
        repos.Should().ContainSingle();
        repos[0].Name.Should().Be("test");
        repos[0].Url.Should().Be("https://example.com/repo.json");
        repos[0].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task AddRepositoryAsync_DuplicateName_ThrowsInvalidOperation()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo1.json");
        Func<Task> act = () => repo.AddRepositoryAsync("test", "https://example.com/repo2.json");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task AddRepositoryAsync_NullName_ThrowsArgumentException()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        Func<Task> act = () => repo.AddRepositoryAsync(null!, "https://example.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddRepositoryAsync_NullUrl_ThrowsArgumentException()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        Func<Task> act = () => repo.AddRepositoryAsync("test", null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddRepositoryAsync_PersistsToDisk()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("persisted", "https://example.com/repo.json");

        string repoFile = Path.Combine(_tempDir, "configurations", "repositories.json");
        File.Exists(repoFile).Should().BeTrue();

        string json = File.ReadAllText(repoFile);
        json.Should().Contain("persisted");
    }

    [Fact]
    public async Task AddRepositoryAsync_FetchesPluginsImmediately()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 3);
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();
        plugins.Should().HaveCount(3);
    }

    [Fact]
    public async Task RemoveRepositoryAsync_RemovesRepository()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");
        await repo.RemoveRepositoryAsync("test");

        repo.GetRepositories().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRepositoryAsync_NotFound_ThrowsInvalidOperation()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        Func<Task> act = () => repo.RemoveRepositoryAsync("nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RefreshAsync_FetchesFromAllEnabledRepos()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 2);
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("repo1", "https://example.com/repo1.json");
        await repo.RefreshAsync();

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();
        plugins.Should().HaveCount(2);
    }

    [Fact]
    public async Task RefreshAsync_FailingRepo_DoesNotThrow()
    {
        HttpClient client = CreateFailingHttpClient();
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        // Manually add a repo without fetching (simulate pre-existing config)
        string configDir = Path.Combine(_tempDir, "configurations");
        string repoConfig = JsonSerializer.Serialize(new List<PluginRepositoryInfo>
        {
            new() { Name = "broken", Url = "https://broken.example.com/repo.json", Enabled = true }
        });
        File.WriteAllText(Path.Combine(configDir, "repositories.json"), repoConfig);

        PluginRepository repo2 = new(client, NullLogger.Instance, _tempDir);
        Func<Task> act = () => repo2.RefreshAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetAvailablePlugins_NoRefresh_ReturnsEmpty()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();

        plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task FindPlugin_ExistingId_ReturnsEntry()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[0].Id;
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");

        PluginRepositoryEntry? found = repo.FindPlugin(pluginId);

        found.Should().NotBeNull();
        found!.Id.Should().Be(pluginId);
    }

    [Fact]
    public void FindPlugin_UnknownId_ReturnsNull()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        PluginRepositoryEntry? found = repo.FindPlugin(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindVersion_ExistingVersion_ReturnsEntry()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[0].Id;
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");

        PluginVersionEntry? found = repo.FindVersion(pluginId, "1.0.0");

        found.Should().NotBeNull();
        found!.Version.Should().Be("1.0.0");
        found.DownloadUrl.Should().Contain("1.0.0");
    }

    [Fact]
    public void FindVersion_UnknownPlugin_ReturnsNull()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        PluginVersionEntry? found = repo.FindVersion(Guid.NewGuid(), "1.0.0");

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindVersion_UnknownVersion_ReturnsNull()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[0].Id;
        HttpClient client = CreateMockHttpClient(manifest);
        PluginRepository repo = new(client, NullLogger.Instance, _tempDir);

        await repo.AddRepositoryAsync("test", "https://example.com/repo.json");

        PluginVersionEntry? found = repo.FindVersion(pluginId, "99.0.0");

        found.Should().BeNull();
    }

    [Fact]
    public void FindVersion_NullVersion_ThrowsArgumentException()
    {
        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        Action act = () => repo.FindVersion(Guid.NewGuid(), null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PluginRepositoryManifest_CanDeserialize()
    {
        string json = """
        {
            "name": "Official Plugins",
            "url": "https://plugins.nomercy.tv/manifest.json",
            "plugins": [
                {
                    "id": "12345678-1234-1234-1234-123456789012",
                    "name": "Scrobbler",
                    "description": "Last.fm scrobbling",
                    "author": "NoMercy",
                    "versions": [
                        {
                            "version": "1.0.0",
                            "targetAbi": "9.0.0",
                            "downloadUrl": "https://plugins.nomercy.tv/scrobbler-1.0.0.zip",
                            "checksum": "abc123",
                            "changelog": "Initial release",
                            "timestamp": "2026-01-01T00:00:00Z"
                        }
                    ]
                }
            ]
        }
        """;

        PluginRepositoryManifest? manifest = JsonSerializer.Deserialize<PluginRepositoryManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be("Official Plugins");
        manifest.Plugins.Should().ContainSingle();
        manifest.Plugins[0].Name.Should().Be("Scrobbler");
        manifest.Plugins[0].Versions.Should().ContainSingle();
        manifest.Plugins[0].Versions[0].Checksum.Should().Be("abc123");
        manifest.Plugins[0].Versions[0].Timestamp.Should().NotBeNull();
    }

    [Fact]
    public void PluginRepositoryEntry_MultipleVersions()
    {
        PluginRepositoryEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Name = "TestPlugin",
            Description = "Test",
            Versions =
            [
                new() { Version = "1.0.0", DownloadUrl = "https://example.com/v1.zip" },
                new() { Version = "2.0.0", DownloadUrl = "https://example.com/v2.zip", TargetAbi = "9.0.0" }
            ]
        };

        entry.Versions.Should().HaveCount(2);
        entry.Versions[1].TargetAbi.Should().Be("9.0.0");
    }

    [Fact]
    public void Constructor_LoadsPersistedRepositories()
    {
        string configDir = Path.Combine(_tempDir, "configurations");
        Directory.CreateDirectory(configDir);

        List<PluginRepositoryInfo> repos =
        [
            new() { Name = "persisted-repo", Url = "https://example.com/persisted.json", Enabled = true }
        ];
        string json = JsonSerializer.Serialize(repos);
        File.WriteAllText(Path.Combine(configDir, "repositories.json"), json);

        PluginRepository repo = new(new(), NullLogger.Instance, _tempDir);

        IReadOnlyList<PluginRepositoryInfo> loaded = repo.GetRepositories();
        loaded.Should().ContainSingle();
        loaded[0].Name.Should().Be("persisted-repo");
    }

    private sealed class MockHttpHandler(string responseContent, HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(responseContent)
            };
            return Task.FromResult(response);
        }
    }
}
