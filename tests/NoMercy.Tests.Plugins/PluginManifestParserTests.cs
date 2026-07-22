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

using System.Text.Json;
using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Storage;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManifestParserTests : IDisposable
{
    private readonly string _tempDir;

    public PluginManifestParserTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-manifest-tests-" + Guid.NewGuid().ToString(format: "N")
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

    private static string CreateValidManifestJson(
        Guid? id = null,
        string name = "TestPlugin",
        string description = "A test plugin",
        string version = "1.0.0",
        string assembly = "TestPlugin.dll",
        string? targetAbi = null,
        string? author = null,
        string? projectUrl = null,
        bool autoEnabled = true
    )
    {
        Dictionary<string, object?> manifest = new()
        {
            [key: "id"] = (id ?? Guid.NewGuid()).ToString(),
            [key: "name"] = name,
            [key: "description"] = description,
            [key: "version"] = version,
            [key: "assembly"] = assembly,
            [key: "autoEnabled"] = autoEnabled,
        };

        if (targetAbi is not null)
            manifest[key: "targetAbi"] = targetAbi;
        if (author is not null)
            manifest[key: "author"] = author;
        if (projectUrl is not null)
            manifest[key: "projectUrl"] = projectUrl;

        return JsonSerializer.Serialize(value: manifest);
    }

    [Fact]
    public void Parse_ValidJson_ReturnsManifest()
    {
        Guid pluginId = Guid.NewGuid();
        string json = CreateValidManifestJson(
            id: pluginId,
            name: "MyPlugin",
            version: "2.1.0",
            assembly: "MyPlugin.dll"
        );

        PluginManifest manifest = PluginManifestParser.Parse(json: json);

        manifest.Id.Should().Be(expected: pluginId);
        manifest.Name.Should().Be(expected: "MyPlugin");
        manifest.Version.Should().Be(expected: "2.1.0");
        manifest.Assembly.Should().Be(expected: "MyPlugin.dll");
        manifest.AutoEnabled.Should().BeTrue();
    }

    [Fact]
    public void Parse_WithOptionalFields_PopulatesAll()
    {
        string json = CreateValidManifestJson(
            author: "Test Author",
            projectUrl: "https://example.com",
            targetAbi: "9.0.0"
        );

        PluginManifest manifest = PluginManifestParser.Parse(json: json);

        manifest.Author.Should().Be(expected: "Test Author");
        manifest.ProjectUrl.Should().Be(expected: "https://example.com");
        manifest.TargetAbi.Should().Be(expected: "9.0.0");
    }

    [Fact]
    public void Parse_AutoEnabledFalse_SetsCorrectly()
    {
        string json = CreateValidManifestJson(autoEnabled: false);

        PluginManifest manifest = PluginManifestParser.Parse(json: json);

        manifest.AutoEnabled.Should().BeFalse();
    }

    [Fact]
    public void Parse_NullJson_ThrowsArgumentException()
    {
        Action act = () => PluginManifestParser.Parse(json: null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_EmptyJson_ThrowsArgumentException()
    {
        Action act = () => PluginManifestParser.Parse(json: "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsJsonException()
    {
        Action act = () => PluginManifestParser.Parse(json: "not json");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_JsonNullLiteral_ThrowsInvalidOperation()
    {
        // "null" is valid, non-whitespace JSON — it clears the
        // ArgumentException.ThrowIfNullOrWhiteSpace guard but deserializes to a
        // null PluginManifest, which must be rejected explicitly rather than
        // let a NullReferenceException surface from Validate().
        Action act = () => PluginManifestParser.Parse(json: "null");

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*deserialize*");
    }

    [Fact]
    public void Parse_EmptyGuid_ThrowsInvalidOperation()
    {
        string json = CreateValidManifestJson(id: Guid.Empty);

        Action act = () => PluginManifestParser.Parse(json: json);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*id*");
    }

    [Fact]
    public void Parse_MissingVersion_ThrowsJsonException()
    {
        string json =
            """{"id":"12345678-1234-1234-1234-123456789012","name":"Test","description":"d","assembly":"t.dll"}""";

        Action act = () => PluginManifestParser.Parse(json: json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_InvalidVersion_ThrowsInvalidOperation()
    {
        string json = CreateValidManifestJson(version: "not-a-version");

        Action act = () => PluginManifestParser.Parse(json: json);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*version*");
    }

    [Fact]
    public void Parse_EmptyAssembly_ThrowsInvalidOperation()
    {
        Guid id = Guid.NewGuid();
        string json =
            $@"{{""id"":""{id}"",""name"":""Test"",""description"":""d"",""version"":""1.0.0"",""assembly"":""""}}";

        Action act = () => PluginManifestParser.Parse(json: json);

        act.Should().Throw<InvalidOperationException>().WithMessage(expectedWildcardPattern: "*assembly*");
    }

    [Fact]
    public void Parse_WithJsonComments_Succeeds()
    {
        Guid id = Guid.NewGuid();
        string json =
            $@"{{
            // This is a comment
            ""id"": ""{id}"",
            ""name"": ""Test"",
            ""description"": ""desc"",
            ""version"": ""1.0.0"",
            ""assembly"": ""Test.dll""
        }}";

        PluginManifest manifest = PluginManifestParser.Parse(json: json);

        manifest.Id.Should().Be(expected: id);
    }

    [Fact]
    public void Parse_WithTrailingCommas_Succeeds()
    {
        Guid id = Guid.NewGuid();
        string json =
            $@"{{
            ""id"": ""{id}"",
            ""name"": ""Test"",
            ""description"": ""desc"",
            ""version"": ""1.0.0"",
            ""assembly"": ""Test.dll"",
        }}";

        PluginManifest manifest = PluginManifestParser.Parse(json: json);

        manifest.Id.Should().Be(expected: id);
    }

    [Fact]
    public async Task ParseFileAsync_ValidFile_ReturnsManifest()
    {
        Guid id = Guid.NewGuid();
        string json = CreateValidManifestJson(id: id, name: "FilePlugin");
        string filePath = Path.Combine(path1: _tempDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: filePath, contents: json);

        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        PluginManifest manifest = await PluginManifestParser.ParseFileAsync(filePath: filePath, storage: storage);

        manifest.Id.Should().Be(expected: id);
        manifest.Name.Should().Be(expected: "FilePlugin");
    }

    [Fact]
    public async Task ParseFileAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        // Missing file UNDER the scoped root. An out-of-root absolute path is
        // rejected by the storage guard before the existence check, and a leading
        // "/" path is absolute on Linux but relative on Windows — so it must stay
        // inside the allowed root to test the not-found path cross-platform.
        string missingPath = Path.Combine(path1: _tempDir, path2: "nonexistent", path3: "plugin.json");
        Func<Task> act = () => PluginManifestParser.ParseFileAsync(filePath: missingPath, storage: storage);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ParseFileAsync_NullPath_ThrowsArgumentException()
    {
        IStorage storage = TestStorageHelper.CreateStorage(rootPath: _tempDir);
        Func<Task> act = () => PluginManifestParser.ParseFileAsync(filePath: null!, storage: storage);

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
            TargetAbi = "9.0.0",
        };

        PluginInfo info = PluginManifestParser.ToPluginInfo(
            manifest: manifest,
            assemblyPath: "/plugins/TestPlugin.dll",
            status: PluginStatus.Active,
            manifestPath: "/plugins/plugin.json"
        );

        info.Id.Should().Be(expected: id);
        info.Name.Should().Be(expected: "TestPlugin");
        info.Description.Should().Be(expected: "A test");
        info.Version.Should().Be(expected: new(major: 2, minor: 0, build: 1));
        info.Status.Should().Be(expected: PluginStatus.Active);
        info.Author.Should().Be(expected: "Author");
        info.ProjectUrl.Should().Be(expected: "https://test.com");
        info.AssemblyPath.Should().Be(expected: "/plugins/TestPlugin.dll");
        info.TargetAbi.Should().Be(expected: "9.0.0");
        info.ManifestPath.Should().Be(expected: "/plugins/plugin.json");
    }

    [Fact]
    public void ToPluginInfo_NullManifest_ThrowsArgumentNullException()
    {
        Action act = () => PluginManifestParser.ToPluginInfo(manifest: null!, assemblyPath: "/path", status: PluginStatus.Active);

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
            Assembly = "Test.dll",
        };

        PluginInfo info = PluginManifestParser.ToPluginInfo(
            manifest: manifest,
            assemblyPath: "/path",
            status: PluginStatus.Disabled
        );

        info.Status.Should().Be(expected: PluginStatus.Disabled);
    }
}
