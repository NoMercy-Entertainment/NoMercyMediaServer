using System.Text.Json;
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
        _tempDir = Path.Combine(Path.GetTempPath(), "nomercy-config-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _config = new PluginConfiguration(_tempDir);
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
        Action act = () => new PluginConfiguration(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_EmptyPath_ThrowsArgumentException()
    {
        Action act = () => new PluginConfiguration("");

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
            Tags = ["tag1", "tag2"]
        };

        _config.SaveConfiguration(saved);

        TestConfig? loaded = _config.GetConfiguration<TestConfig>();

        loaded.Should().NotBeNull();
        loaded!.ApiKey.Should().Be("test-key-123");
        loaded.MaxRetries.Should().Be(5);
        loaded.Enabled.Should().BeFalse();
        loaded.Tags.Should().BeEquivalentTo(["tag1", "tag2"]);
    }

    [Fact]
    public void SaveConfiguration_CreatesFile()
    {
        _config.HasConfiguration().Should().BeFalse();

        _config.SaveConfiguration(new TestConfig { ApiKey = "key" });

        _config.HasConfiguration().Should().BeTrue();
    }

    [Fact]
    public void SaveConfiguration_WritesFormattedJson()
    {
        _config.SaveConfiguration(new TestConfig { ApiKey = "key" });

        string filePath = Path.Combine(_tempDir, "config.json");
        string json = File.ReadAllText(filePath);

        json.Should().Contain("\n");
        json.Should().Contain("ApiKey");
    }

    [Fact]
    public void SaveConfiguration_NullConfig_ThrowsArgumentNullException()
    {
        Action act = () => _config.SaveConfiguration<TestConfig>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SaveConfiguration_Overwrites_ExistingConfig()
    {
        _config.SaveConfiguration(new TestConfig { ApiKey = "old" });
        _config.SaveConfiguration(new TestConfig { ApiKey = "new" });

        TestConfig? loaded = _config.GetConfiguration<TestConfig>();
        loaded!.ApiKey.Should().Be("new");
    }

    [Fact]
    public void DeleteConfiguration_RemovesFile()
    {
        _config.SaveConfiguration(new TestConfig { ApiKey = "key" });
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
            Tags = ["async-tag"]
        };

        await _config.SaveConfigurationAsync(saved);
        TestConfig? loaded = await _config.GetConfigurationAsync<TestConfig>();

        loaded.Should().NotBeNull();
        loaded!.ApiKey.Should().Be("async-key");
        loaded.MaxRetries.Should().Be(10);
        loaded.Tags.Should().ContainSingle().Which.Should().Be("async-tag");
    }

    [Fact]
    public async Task SaveConfigurationAsync_NullConfig_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _config.SaveConfigurationAsync<TestConfig>(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void SaveConfiguration_CreatesDirectoryIfNeeded()
    {
        string nestedDir = Path.Combine(_tempDir, "nested", "deep");
        PluginConfiguration nestedConfig = new(nestedDir);

        nestedConfig.SaveConfiguration(new TestConfig { ApiKey = "nested" });

        nestedConfig.HasConfiguration().Should().BeTrue();
        TestConfig? loaded = nestedConfig.GetConfiguration<TestConfig>();
        loaded!.ApiKey.Should().Be("nested");
    }

    [Fact]
    public void GetConfiguration_DifferentType_DeserializesCorrectly()
    {
        _config.SaveConfiguration(new OtherConfig { Name = "test", Score = 9.5 });

        OtherConfig? loaded = _config.GetConfiguration<OtherConfig>();

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("test");
        loaded.Score.Should().Be(9.5);
    }

    [Fact]
    public void IPluginConfiguration_Interface_IsImplemented()
    {
        IPluginConfiguration config = _config;

        config.Should().NotBeNull();
        config.HasConfiguration().Should().BeFalse();
    }
}
