using System.Text.Json;
using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManifestParserTests : IDisposable
{
    private readonly string _tempDir;

    public PluginManifestParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nomercy-manifest-tests-" + Guid.NewGuid().ToString("N"));
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

    private static string CreateValidManifestJson(
        Guid? id = null,
        string name = "TestPlugin",
        string description = "A test plugin",
        string version = "1.0.0",
        string assembly = "TestPlugin.dll",
        string? targetAbi = null,
        string? author = null,
        string? projectUrl = null,
        bool autoEnabled = true)
    {
        Dictionary<string, object?> manifest = new()
        {
            ["id"] = (id ?? Guid.NewGuid()).ToString(),
            ["name"] = name,
            ["description"] = description,
            ["version"] = version,
            ["assembly"] = assembly,
            ["autoEnabled"] = autoEnabled
        };

        if (targetAbi is not null) manifest["targetAbi"] = targetAbi;
        if (author is not null) manifest["author"] = author;
        if (projectUrl is not null) manifest["projectUrl"] = projectUrl;

        return JsonSerializer.Serialize(manifest);
    }

    [Fact]
    public void Parse_ValidJson_ReturnsManifest()
    {
        Guid pluginId = Guid.NewGuid();
        string json = CreateValidManifestJson(id: pluginId, name: "MyPlugin", version: "2.1.0", assembly: "MyPlugin.dll");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().Be(pluginId);
        manifest.Name.Should().Be("MyPlugin");
        manifest.Version.Should().Be("2.1.0");
        manifest.Assembly.Should().Be("MyPlugin.dll");
        manifest.AutoEnabled.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithOptionalFields_PopulatesAll()
    {
        string json = CreateValidManifestJson(
            author: "Test Author",
            projectUrl: "https://example.com",
            targetAbi: "9.0.0");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Author.Should().Be("Test Author");
        manifest.ProjectUrl.Should().Be("https://example.com");
        manifest.TargetAbi.Should().Be("9.0.0");
    }

    [Fact]
    public void Parse_AutoEnabledFalse_SetsCorrectly()
    {
        string json = CreateValidManifestJson(autoEnabled: false);

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.AutoEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_NullJson_ThrowsArgumentException()
    {
        Action act = () => PluginManifestParser.Parse(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyJson_ThrowsArgumentException()
    {
        Action act = () => PluginManifestParser.Parse("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Action act = () => PluginManifestParser.Parse("not json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_EmptyGuid_ThrowsInvalidOperation()
    {
        string json = CreateValidManifestJson(id: Guid.Empty);

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*id*");
    }

    [Fact]
    public void Parse_MissingVersion_ThrowsJsonException()
    {
        string json = """{"id":"12345678-1234-1234-1234-123456789012","name":"Test","description":"d","assembly":"t.dll"}""";

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_InvalidVersion_ThrowsInvalidOperation()
    {
        string json = CreateValidManifestJson(version: "not-a-version");

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*version*");
    }

    [Fact]
    public void Parse_EmptyAssembly_ThrowsInvalidOperation()
    {
        Guid id = Guid.NewGuid();
        string json = $@"{{""id"":""{id}"",""name"":""Test"",""description"":""d"",""version"":""1.0.0"",""assembly"":""""}}";

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*assembly*");
    }

    [Fact]
    public void Parse_WithJsonComments_Succeeds()
    {
        Guid id = Guid.NewGuid();
        string json = $@"{{
            // This is a comment
            ""id"": ""{id}"",
            ""name"": ""Test"",
            ""description"": ""desc"",
            ""version"": ""1.0.0"",
            ""assembly"": ""Test.dll""
        }}";

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().Be(id);
    }

    [Fact]
    public void Parse_WithTrailingCommas_Succeeds()
    {
        Guid id = Guid.NewGuid();
        string json = $@"{{
            ""id"": ""{id}"",
            ""name"": ""Test"",
            ""description"": ""desc"",
            ""version"": ""1.0.0"",
            ""assembly"": ""Test.dll"",
        }}";

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().Be(id);
    }

    [Fact]
    public async Task ParseFileAsync_ValidFile_ReturnsManifest()
    {
        Guid id = Guid.NewGuid();
        string json = CreateValidManifestJson(id: id, name: "FilePlugin");
        string filePath = Path.Combine(_tempDir, "plugin.json");
        await File.WriteAllTextAsync(filePath, json);

        PluginManifest manifest = await PluginManifestParser.ParseFileAsync(filePath);

        manifest.Id.Should().Be(id);
        manifest.Name.Should().Be("FilePlugin");
    }

    [Fact]
    public async Task ParseFileAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        Func<Task> act = () => PluginManifestParser.ParseFileAsync("/nonexistent/plugin.json");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ParseFileAsync_NullPath_ThrowsArgumentException()
    {
        Func<Task> act = () => PluginManifestParser.ParseFileAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ToPluginInfo_CreatesCorrectInfo()
    {
        Guid id = Guid.NewGuid();
        PluginManifest manifest = new()
        {
            Id = id,
            Name = "TestPlugin",
            Description = "A test",
            Version = "2.0.1",
            Assembly = "TestPlugin.dll",
            Author = "Author",
            ProjectUrl = "https://test.com",
            TargetAbi = "9.0.0"
        };

        PluginInfo info = PluginManifestParser.ToPluginInfo(manifest, "/plugins/TestPlugin.dll", PluginStatus.Active, "/plugins/plugin.json");

        info.Id.Should().Be(id);
        info.Name.Should().Be("TestPlugin");
        info.Description.Should().Be("A test");
        info.Version.Should().Be(new Version(2, 0, 1));
        info.Status.Should().Be(PluginStatus.Active);
        info.Author.Should().Be("Author");
        info.ProjectUrl.Should().Be("https://test.com");
        info.AssemblyPath.Should().Be("/plugins/TestPlugin.dll");
        info.TargetAbi.Should().Be("9.0.0");
        info.ManifestPath.Should().Be("/plugins/plugin.json");
    }

    [Fact]
    public void ToPluginInfo_NullManifest_ThrowsArgumentNullException()
    {
        Action act = () => PluginManifestParser.ToPluginInfo(null!, "/path", PluginStatus.Active);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToPluginInfo_DisabledStatus_SetsCorrectly()
    {
        PluginManifest manifest = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Description = "d",
            Version = "1.0.0",
            Assembly = "Test.dll"
        };

        PluginInfo info = PluginManifestParser.ToPluginInfo(manifest, "/path", PluginStatus.Disabled);

        info.Status.Should().Be(PluginStatus.Disabled);
    }
}
