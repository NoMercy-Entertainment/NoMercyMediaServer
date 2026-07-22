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

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginRepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public PluginRepositoryTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-repo-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path: _tempDir))
            {
                Directory.Delete(path: _tempDir, recursive: true);
            }
        }
        catch (IOException) { }
    }

    private static PluginRepositoryManifest CreateTestManifest(
        string name = "test-repo",
        int pluginCount = 2
    )
    {
        List<PluginRepositoryEntry> plugins = [];
        for (int i = 0; i < pluginCount; i++)
        {
            plugins.Add(
                item: new()
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
                            Changelog = "Initial release",
                        },
                    ],
                }
            );
        }

        return new()
        {
            Name = name,
            Url = "https://example.com/repo",
            Plugins = plugins,
        };
    }

    private static HttpClient CreateMockHttpClient(PluginRepositoryManifest manifest)
    {
        string json = JsonSerializer.Serialize(value: manifest);
        MockHttpHandler handler = new(responseContent: json, statusCode: HttpStatusCode.OK);
        return new(handler: handler);
    }

    private static HttpClient CreateFailingHttpClient()
    {
        MockHttpHandler handler = new(responseContent: "", statusCode: HttpStatusCode.InternalServerError);
        return new(handler: handler);
    }

    private PluginRepository MakeRepo(HttpClient? client = null)
    {
        IStorageDriver driver = TestStorageHelper.CreateBackend();
        IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [_tempDir], driver: driver));
        return new(httpClient: client ?? new HttpClient(), logger: NullLogger.Instance, pluginsPath: _tempDir, storage: storage);
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        Action act = () => new PluginRepository(httpClient: null!, logger: NullLogger.Instance, pluginsPath: _tempDir, storage: storage);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        Action act = () => new PluginRepository(httpClient: new(), logger: null!, pluginsPath: _tempDir, storage: storage);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentException()
    {
        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        Action act = () => new PluginRepository(httpClient: new(), logger: NullLogger.Instance, pluginsPath: null!, storage: storage);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullStorage_ThrowsArgumentNullException()
    {
        Action act = () => new PluginRepository(httpClient: new(), logger: NullLogger.Instance, pluginsPath: _tempDir, storage: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_CreatesConfigurationsDirectory()
    {
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");

        _ = MakeRepo();

        Directory.Exists(path: configDir).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ConfigurationsDirectoryAlreadyExists_DoesNotThrow()
    {
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");
        Directory.CreateDirectory(path: configDir);

        Action act = () => MakeRepo();

        act.Should().NotThrow();
        Directory.Exists(path: configDir).Should().BeTrue();
    }

    [Fact]
    public void GetRepositories_Empty_ReturnsEmptyList()
    {
        PluginRepository repo = MakeRepo();

        IReadOnlyList<PluginRepositoryInfo> repos = repo.GetRepositories();

        repos.Should().BeEmpty();
    }

    [Fact]
    public async Task AddRepositoryAsync_AddsRepository()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        IReadOnlyList<PluginRepositoryInfo> repos = repo.GetRepositories();
        repos.Should().ContainSingle();
        repos[index: 0].Name.Should().Be(expected: "test");
        repos[index: 0].Url.Should().Be(expected: "https://example.com/repo.json");
        repos[index: 0].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task AddRepositoryAsync_DuplicateName_ThrowsInvalidOperation()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo1.json");
        Func<Task> act = () => repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo2.json");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*already exists*");
    }

    [Fact]
    public async Task AddRepositoryAsync_NullName_ThrowsArgumentException()
    {
        PluginRepository repo = MakeRepo();

        Func<Task> act = () => repo.AddRepositoryAsync(name: null!, url: "https://example.com");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddRepositoryAsync_NullUrl_ThrowsArgumentException()
    {
        PluginRepository repo = MakeRepo();

        Func<Task> act = () => repo.AddRepositoryAsync(name: "test", url: null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddRepositoryAsync_PersistsToDisk()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "persisted", url: "https://example.com/repo.json");

        string repoFile = Path.Combine(path1: _tempDir, path2: "configurations", path3: "repositories.json");
        File.Exists(path: repoFile).Should().BeTrue();

        string json = await File.ReadAllTextAsync(path: repoFile);
        json.Should().Contain(expected: "persisted");
    }

    [Fact]
    public async Task AddRepositoryAsync_FetchesPluginsImmediately()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 3);
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();
        plugins.Should().HaveCount(expected: 3);
    }

    [Fact]
    public async Task RemoveRepositoryAsync_RemovesRepository()
    {
        PluginRepositoryManifest manifest = CreateTestManifest();
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");
        await repo.RemoveRepositoryAsync(name: "test");

        repo.GetRepositories().Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRepositoryAsync_NotFound_ThrowsInvalidOperation()
    {
        PluginRepository repo = MakeRepo();

        Func<Task> act = () => repo.RemoveRepositoryAsync(name: "nonexistent");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*not found*");
    }

    [Fact]
    public async Task RefreshAsync_FetchesFromAllEnabledRepos()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 2);
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "repo1", url: "https://example.com/repo1.json");
        await repo.RefreshAsync();

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();
        plugins.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task RefreshAsync_FailingRepo_DoesNotThrow()
    {
        HttpClient client = CreateFailingHttpClient();
        PluginRepository repo = MakeRepo(client: client);

        // Manually add a repo without fetching (simulate pre-existing config)
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");
        string repoConfig = JsonSerializer.Serialize(
            value: new List<PluginRepositoryInfo>
            {
                new()
                {
                    Name = "broken",
                    Url = "https://broken.example.com/repo.json",
                    Enabled = true,
                },
            }
        );
        await File.WriteAllTextAsync(path: Path.Combine(path1: configDir, path2: "repositories.json"), contents: repoConfig);

        PluginRepository repo2 = MakeRepo(client: client);
        Func<Task> act = () => repo2.RefreshAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshAsync_OneRepoFailsAnotherSucceeds_SkipsFailingRepoKeepsOthers()
    {
        // RefreshAsync_FailingRepo_DoesNotThrow above builds repo2 through the sync
        // constructor, which never loads _repositories from disk — its enabled-repo
        // loop is empty and the per-repo catch (RefreshAsync's own, not
        // RefreshRepositoryAsync's) is never actually entered. This test adds two
        // repos through the real AddRepositoryAsync path so RefreshAsync's loop has
        // two live entries, one of which fails per request, proving the failure is
        // isolated to that repo and the other repo's plugins still come through.
        PluginRepositoryManifest goodManifest = CreateTestManifest(
            name: "good-repo",
            pluginCount: 2
        );
        RoutingHttpHandler handler = new(
            okUrl: "https://good.example.com/repo.json",
            okJson: JsonSerializer.Serialize(value: goodManifest)
        );
        HttpClient client = new(handler: handler);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "good", url: "https://good.example.com/repo.json");
        await repo.AddRepositoryAsync(name: "broken", url: "https://broken.example.com/repo.json");

        Func<Task> act = () => repo.RefreshAsync();

        await act.Should().NotThrowAsync();
        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();
        plugins.Should().HaveCount(expected: 2);
    }

    [Fact]
    public async Task AddRepositoryAsync_ConfigurationsPathReplacedByAFile_SaveFailsSilently()
    {
        // SaveRepositoriesToDiskAsync's own catch(Exception): WriteAllTextAsync
        // auto-creates a missing parent directory, so simply deleting
        // "configurations" would just have it silently recreated. Replacing it
        // with a FILE of the same name makes Directory.CreateDirectory itself
        // throw IOException ("a file with the same name already exists"),
        // which is the only way to force this specific write to fail — proving
        // AddRepositoryAsync still completes (the repo entry stands in memory)
        // instead of propagating the disk failure to the caller.
        PluginRepository repo = MakeRepo(client: CreateFailingHttpClient());
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");
        Directory.Delete(path: configDir, recursive: true);
        File.WriteAllText(path: configDir, contents: "blocking file");

        Func<Task> act = () => repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        await act.Should().NotThrowAsync();
        repo.GetRepositories().Should().ContainSingle();
    }

    [Fact]
    public async Task AddRepositoryAsync_FetchFails_StillAddsRepository_ButNoPluginsAvailable()
    {
        HttpClient client = CreateFailingHttpClient();
        PluginRepository repo = MakeRepo(client: client);

        Func<Task> act = () => repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        await act.Should().NotThrowAsync();
        repo.GetRepositories().Should().ContainSingle().Which.Name.Should().Be(expected: "test");
        repo.GetAvailablePlugins().Should().BeEmpty();
    }

    [Fact]
    public async Task FetchRepositoryPluginsAsync_ResponseBodyIsJsonNull_ReturnsEmptyList()
    {
        MockHttpHandler handler = new(responseContent: "null", statusCode: HttpStatusCode.OK);
        HttpClient client = new(handler: handler);
        PluginRepository repo = MakeRepo(client: client);

        List<PluginRepositoryEntry> plugins = await repo.FetchRepositoryPluginsAsync(
            url: "https://example.com/repo.json"
        );

        plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_NoRepositoriesFileOnDisk_ReturnsEmptyWithoutThrowing()
    {
        IStorageDriver driver = TestStorageHelper.CreateBackend();
        IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [_tempDir], driver: driver));

        // An unhandled exception here fails the test — the assertion below is
        // reached only when CreateAsync completed without throwing.
        PluginRepository repo = await PluginRepository.CreateAsync(
            httpClient: new(),
            logger: NullLogger.Instance,
            pluginsPath: _tempDir,
            storage: storage
        );

        repo.GetRepositories().Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_MalformedRepositoriesFileOnDisk_ReturnsEmptyWithoutThrowing()
    {
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");
        Directory.CreateDirectory(path: configDir);
        await File.WriteAllTextAsync(
            path: Path.Combine(path1: configDir, path2: "repositories.json"),
            contents: "not valid json {{{{"
        );

        IStorageDriver driver = TestStorageHelper.CreateBackend();
        IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [_tempDir], driver: driver));

        PluginRepository repo = await PluginRepository.CreateAsync(
            httpClient: new(),
            logger: NullLogger.Instance,
            pluginsPath: _tempDir,
            storage: storage
        );

        repo.GetRepositories().Should().BeEmpty();
    }

    [Fact]
    public void GetAvailablePlugins_NoRefresh_ReturnsEmpty()
    {
        PluginRepository repo = MakeRepo();

        IReadOnlyList<PluginRepositoryEntry> plugins = repo.GetAvailablePlugins();

        plugins.Should().BeEmpty();
    }

    [Fact]
    public async Task FindPlugin_ExistingId_ReturnsEntry()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[index: 0].Id;
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        PluginRepositoryEntry? found = repo.FindPlugin(pluginId: pluginId);

        found.Should().NotBeNull();
        found!.Id.Should().Be(expected: pluginId);
    }

    [Fact]
    public void FindPlugin_UnknownId_ReturnsNull()
    {
        PluginRepository repo = MakeRepo();

        PluginRepositoryEntry? found = repo.FindPlugin(pluginId: Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindVersion_ExistingVersion_ReturnsEntry()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[index: 0].Id;
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        PluginVersionEntry? found = repo.FindVersion(pluginId: pluginId, version: "1.0.0");

        found.Should().NotBeNull();
        found!.Version.Should().Be(expected: "1.0.0");
        found.DownloadUrl.Should().Contain(expected: "1.0.0");
    }

    [Fact]
    public void FindVersion_UnknownPlugin_ReturnsNull()
    {
        PluginRepository repo = MakeRepo();

        PluginVersionEntry? found = repo.FindVersion(pluginId: Guid.NewGuid(), version: "1.0.0");

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindVersion_UnknownVersion_ReturnsNull()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 1);
        Guid pluginId = manifest.Plugins[index: 0].Id;
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);

        await repo.AddRepositoryAsync(name: "test", url: "https://example.com/repo.json");

        PluginVersionEntry? found = repo.FindVersion(pluginId: pluginId, version: "99.0.0");

        found.Should().BeNull();
    }

    [Fact]
    public void FindVersion_NullVersion_ThrowsArgumentException()
    {
        PluginRepository repo = MakeRepo();

        Action act = () => repo.FindVersion(pluginId: Guid.NewGuid(), version: null!);

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

        PluginRepositoryManifest? manifest = JsonSerializer.Deserialize<PluginRepositoryManifest>(
            json: json,
            options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        manifest.Should().NotBeNull();
        manifest!.Name.Should().Be(expected: "Official Plugins");
        manifest.Plugins.Should().ContainSingle();
        manifest.Plugins[index: 0].Name.Should().Be(expected: "Scrobbler");
        manifest.Plugins[index: 0].Versions.Should().ContainSingle();
        manifest.Plugins[index: 0].Versions[index: 0].Checksum.Should().Be(expected: "abc123");
        manifest.Plugins[index: 0].Versions[index: 0].Timestamp.Should().NotBeNull();
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
                new()
                {
                    Version = "2.0.0",
                    DownloadUrl = "https://example.com/v2.zip",
                    TargetAbi = "9.0.0",
                },
            ],
        };

        entry.Versions.Should().HaveCount(expected: 2);
        entry.Versions[index: 1].TargetAbi.Should().Be(expected: "9.0.0");
    }

    [Fact]
    public async Task CreateAsync_LoadsPersistedRepositories()
    {
        string configDir = Path.Combine(path1: _tempDir, path2: "configurations");
        Directory.CreateDirectory(path: configDir);

        List<PluginRepositoryInfo> repos =
        [
            new()
            {
                Name = "persisted-repo",
                Url = "https://example.com/persisted.json",
                Enabled = true,
            },
        ];
        string json = JsonSerializer.Serialize(value: repos);
        File.WriteAllText(path: Path.Combine(path1: configDir, path2: "repositories.json"), contents: json);

        IStorageDriver driver = TestStorageHelper.CreateBackend();
        IStorage storage = new LocalStorage(driver: driver, guard: new(allowedRoots: [_tempDir], driver: driver));
        PluginRepository repo = await PluginRepository.CreateAsync(
            httpClient: new(),
            logger: NullLogger.Instance,
            pluginsPath: _tempDir,
            storage: storage
        );

        IReadOnlyList<PluginRepositoryInfo> loaded = repo.GetRepositories();
        loaded.Should().ContainSingle();
        loaded[index: 0].Name.Should().Be(expected: "persisted-repo");
    }

    // ── Concurrent save ───────────────────────────────────────────────────────
    //
    // SaveRepositoriesToDiskAsync used to serialize the live _repositories
    // list without holding _lock — a concurrent AddRepositoryAsync mutating
    // that same list (each call locks only around its own Add, then saves
    // outside the lock) could race the serializer's enumeration and throw
    // "Collection was modified; enumeration operation may not execute."

    [Fact]
    public async Task AddRepositoryAsync_ConcurrentCalls_DoNotThrowFromRacingSave()
    {
        PluginRepositoryManifest manifest = CreateTestManifest(pluginCount: 0);
        HttpClient client = CreateMockHttpClient(manifest: manifest);
        PluginRepository repo = MakeRepo(client: client);
        const int concurrentAdds = 20;

        Func<Task> act = () =>
            Task.WhenAll(
                tasks: Enumerable
                    .Range(start: 0, count: concurrentAdds)
                    .Select(selector: i =>
                        repo.AddRepositoryAsync(name: $"repo-{i}", url: $"https://example.com/repo-{i}.json")
                    )
            );

        await act.Should().NotThrowAsync();

        IReadOnlyList<PluginRepositoryInfo> repos = repo.GetRepositories();
        repos.Should().HaveCount(expected: concurrentAdds);
    }

    private sealed class MockHttpHandler(string responseContent, HttpStatusCode statusCode)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            HttpResponseMessage response = new(statusCode: statusCode)
            {
                Content = new StringContent(content: responseContent),
            };
            return Task.FromResult(result: response);
        }
    }

    // Returns okJson for the one URL matching okUrl and a 500 for every other
    // URL — lets a single HttpClient represent "one repo fetch succeeds, every
    // other repo fetch fails" within the same PluginRepository instance.
    private sealed class RoutingHttpHandler(string okUrl, string okJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            bool isOk = string.Equals(
                a: request.RequestUri?.ToString(),
                b: okUrl,
                comparisonType: StringComparison.Ordinal
            );

            HttpResponseMessage response = new(
                statusCode: isOk ? HttpStatusCode.OK : HttpStatusCode.InternalServerError
            )
            {
                Content = new StringContent(content: isOk ? okJson : string.Empty),
            };
            return Task.FromResult(result: response);
        }
    }
}
