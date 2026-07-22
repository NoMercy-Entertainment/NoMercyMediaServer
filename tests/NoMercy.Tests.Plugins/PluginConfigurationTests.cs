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

using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginConfigurationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PluginConfiguration _config;

    public PluginConfigurationTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-config-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempDir);
        _config = new(dataFolderPath: _tempDir, storage: TestStorageHelper.CreateStorage(rootPath: _tempDir));
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

    private sealed class TestConfig
    {
        public string ApiKey { get; set; } = "";
        public int MaxRetries { get; set; } = 3;
        public bool Enabled { get; set; } = true;
        public List<string> Tags { get; set; } = [];
    }

    private sealed class OtherConfig
    {
        public string Name { get; set; } = "";
        public double Score { get; set; }
    }

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentException()
    {
        Action act = () =>
            new PluginConfiguration(dataFolderPath: null!, storage: TestStorageHelper.CreateStorage(rootPath: _tempDir));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        Action act = () => new PluginConfiguration(dataFolderPath: "", storage: TestStorageHelper.CreateStorage(rootPath: _tempDir));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HasConfiguration_NoFile_ReturnsFalse()
    {
        bool result = _config.HasConfiguration();

        result.Should().BeFalse();
    }

    [Fact]
    public void GetConfiguration_NoFile_ReturnsNull()
    {
        TestConfig? result = _config.GetConfiguration<TestConfig>();

        result.Should().BeNull();
    }

    [Fact]
    public void SaveConfiguration_ThenGet_RoundTrips()
    {
        TestConfig saved = new()
        {
            ApiKey = "test-key-123",
            MaxRetries = 5,
            Enabled = false,
            Tags = ["tag1", "tag2"],
        };

        _config.SaveConfiguration(configuration: saved);

        TestConfig? loaded = _config.GetConfiguration<TestConfig>();

        loaded.Should().NotBeNull();
        loaded!.ApiKey.Should().Be(expected: "test-key-123");
        loaded.MaxRetries.Should().Be(expected: 5);
        loaded.Enabled.Should().BeFalse();
        loaded.Tags.Should().BeEquivalentTo(expectation: ["tag1", "tag2"]);
    }

    [Fact]
    public void SaveConfiguration_CreatesFile()
    {
        _config.HasConfiguration().Should().BeFalse();

        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "key" });

        _config.HasConfiguration().Should().BeTrue();
    }

    [Fact]
    public void SaveConfiguration_WritesFormattedJson()
    {
        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "key" });

        string filePath = Path.Combine(path1: _tempDir, path2: "config.json");
        string json = File.ReadAllText(path: filePath);

        json.Should().Contain(expected: "\n");
        json.Should().Contain(expected: "ApiKey");
    }

    [Fact]
    public void SaveConfiguration_NullConfig_ThrowsArgumentNullException()
    {
        Action act = () => _config.SaveConfiguration<TestConfig>(configuration: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SaveConfiguration_Overwrites_ExistingConfig()
    {
        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "old" });
        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "new" });

        TestConfig? loaded = _config.GetConfiguration<TestConfig>();
        loaded!.ApiKey.Should().Be(expected: "new");
    }

    [Fact]
    public void DeleteConfiguration_RemovesFile()
    {
        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "key" });
        _config.HasConfiguration().Should().BeTrue();

        _config.DeleteConfiguration();

        _config.HasConfiguration().Should().BeFalse();
    }

    [Fact]
    public void DeleteConfiguration_NoFile_DoesNotThrow()
    {
        Action act = () => _config.DeleteConfiguration();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetConfigurationAsync_NoFile_ReturnsNull()
    {
        TestConfig? result = await _config.GetConfigurationAsync<TestConfig>();

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveConfigurationAsync_ThenGetAsync_RoundTrips()
    {
        TestConfig saved = new()
        {
            ApiKey = "async-key",
            MaxRetries = 10,
            Enabled = true,
            Tags = ["async-tag"],
        };

        await _config.SaveConfigurationAsync(configuration: saved);
        TestConfig? loaded = await _config.GetConfigurationAsync<TestConfig>();

        loaded.Should().NotBeNull();
        loaded!.ApiKey.Should().Be(expected: "async-key");
        loaded.MaxRetries.Should().Be(expected: 10);
        loaded.Tags.Should().ContainSingle().Which.Should().Be(expected: "async-tag");
    }

    [Fact]
    public async Task SaveConfigurationAsync_NullConfig_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _config.SaveConfigurationAsync<TestConfig>(configuration: null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void SaveConfiguration_CreatesDirectoryIfNeeded()
    {
        string nestedDir = Path.Combine(path1: _tempDir, path2: "nested", path3: "deep");
        Directory.CreateDirectory(path: nestedDir);
        PluginConfiguration nestedConfig = new(
            dataFolderPath: nestedDir,
            storage: TestStorageHelper.CreateStorage(rootPath: nestedDir)
        );

        nestedConfig.SaveConfiguration(configuration: new TestConfig { ApiKey = "nested" });

        nestedConfig.HasConfiguration().Should().BeTrue();
        TestConfig? loaded = nestedConfig.GetConfiguration<TestConfig>();
        loaded!.ApiKey.Should().Be(expected: "nested");
    }

    [Fact]
    public void SaveConfiguration_DirectoryDoesNotExistYet_CreatesItBeforeWriting()
    {
        // Unlike SaveConfiguration_CreatesDirectoryIfNeeded above, the target
        // directory is NEVER pre-created here — this is the only way to reach
        // the storage.CreateDirectory(directory) call itself rather than just
        // the surrounding existence check.
        string neverCreatedDir = Path.Combine(path1: _tempDir, path2: "missing", path3: "nested");
        PluginConfiguration config = new(
            dataFolderPath: neverCreatedDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempDir)
        );

        config.SaveConfiguration(configuration: new TestConfig { ApiKey = "created-on-demand" });

        Directory.Exists(path: neverCreatedDir).Should().BeTrue();
        config.HasConfiguration().Should().BeTrue();
    }

    [Fact]
    public async Task SaveConfigurationAsync_DirectoryDoesNotExistYet_CreatesItBeforeWriting()
    {
        string neverCreatedDir = Path.Combine(path1: _tempDir, path2: "missing-async", path3: "nested");
        PluginConfiguration config = new(
            dataFolderPath: neverCreatedDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempDir)
        );

        await config.SaveConfigurationAsync(configuration: new TestConfig { ApiKey = "created-on-demand-async" });

        Directory.Exists(path: neverCreatedDir).Should().BeTrue();
        config.HasConfiguration().Should().BeTrue();
    }

    [Fact]
    public void GetConfiguration_MalformedJsonOnDisk_ReturnsNullInsteadOfThrowing()
    {
        // Plugin config files can drift to malformed JSON across upgrades or
        // crashes mid-write. TryDeserialize must treat that as "no config" so
        // the plugin can re-initialise with defaults instead of taking the
        // load path down with an unhandled JsonException.
        string filePath = Path.Combine(path1: _tempDir, path2: "config.json");
        File.WriteAllText(path: filePath, contents: "{ not valid json ][");

        TestConfig? result = _config.GetConfiguration<TestConfig>();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigurationAsync_MalformedJsonOnDisk_ReturnsNullInsteadOfThrowing()
    {
        string filePath = Path.Combine(path1: _tempDir, path2: "config.json");
        await File.WriteAllTextAsync(path: filePath, contents: "{ not valid json ][");

        TestConfig? result = await _config.GetConfigurationAsync<TestConfig>();

        result.Should().BeNull();
    }

    [Fact]
    public void GetConfiguration_DifferentType_DeserializesCorrectly()
    {
        _config.SaveConfiguration(configuration: new OtherConfig { Name = "test", Score = 9.5 });

        OtherConfig? loaded = _config.GetConfiguration<OtherConfig>();

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be(expected: "test");
        loaded.Score.Should().Be(expected: 9.5);
    }

    [Fact]
    public void IPluginConfiguration_Interface_IsImplemented()
    {
        IPluginConfiguration config = _config;

        config.Should().NotBeNull();
        config.HasConfiguration().Should().BeFalse();
    }

    // ── Sync/async mutual exclusion ──────────────────────────────────────────
    //
    // GetConfigurationAsync/SaveConfigurationAsync used to run with NO lock at
    // all while their sync counterparts held one — two concurrent writers
    // (one sync, one async) could both have the config file open for write at
    // once. LocalStorage opens for write without shared access, so a genuine
    // race throws IOException; these reproduce that concurrency shape.

    [Fact]
    public async Task SaveConfigurationAsync_ConcurrentCalls_DoNotThrow()
    {
        const int concurrentWrites = 30;

        Func<Task> act = () =>
            Task.WhenAll(
                tasks: Enumerable
                    .Range(start: 0, count: concurrentWrites)
                    .Select(selector: i =>
                        _config.SaveConfigurationAsync(configuration: new TestConfig { ApiKey = $"key-{i}" })
                    )
            );

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveConfiguration_And_SaveConfigurationAsync_Concurrently_DoNotThrow()
    {
        const int iterations = 30;

        Task syncTask = Task.Run(action: () =>
        {
            for (int i = 0; i < iterations; i++)
                _config.SaveConfiguration(configuration: new TestConfig { ApiKey = $"sync-{i}" });
        });
        Task asyncTask = Task.Run(function: async () =>
        {
            for (int i = 0; i < iterations; i++)
                await _config.SaveConfigurationAsync(configuration: new TestConfig { ApiKey = $"async-{i}" });
        });

        Func<Task> act = () => Task.WhenAll(tasks: [syncTask, asyncTask]);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetConfigurationAsync_ConcurrentWithSaveConfiguration_DoesNotThrow()
    {
        _config.SaveConfiguration(configuration: new TestConfig { ApiKey = "seed" });
        const int iterations = 30;

        Task writer = Task.Run(action: () =>
        {
            for (int i = 0; i < iterations; i++)
                _config.SaveConfiguration(configuration: new TestConfig { ApiKey = $"writer-{i}" });
        });
        Task reader = Task.Run(function: async () =>
        {
            for (int i = 0; i < iterations; i++)
                await _config.GetConfigurationAsync<TestConfig>();
        });

        Func<Task> act = () => Task.WhenAll(tasks: [writer, reader]);

        await act.Should().NotThrowAsync();
    }
}
