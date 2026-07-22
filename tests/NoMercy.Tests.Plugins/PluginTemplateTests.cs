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
using Xunit;

namespace NoMercy.Tests.Plugins;

[Trait(name: "Category", value: "Unit")]
public class PluginTemplateTests
{
    private static readonly string TemplateRoot = FindRepoPath(
        relativePath: Path.Combine(path1: "templates", path2: "NoMercy.Plugin.Template")
    );

    // Walk up from the test assembly instead of a fixed ".." chain — the output
    // directory depth changes under a redirected BaseOutputPath.
    private static string FindRepoPath(string relativePath)
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null!)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (Path.Exists(path: candidate))
                return candidate;

            dir = Path.GetDirectoryName(path: dir)!;
        }

        throw new FileNotFoundException(
            message: $"Could not locate {relativePath} above {AppContext.BaseDirectory}"
        );
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    [Fact]
    public void TemplateDirectory_Exists()
    {
        Directory
            .Exists(path: TemplateRoot)
            .Should()
            .BeTrue(because: $"Template directory should exist at {TemplateRoot}");
    }

    [Fact]
    public void TemplateConfig_Exists_AndIsValidJson()
    {
        string configPath = Path.Combine(path1: TemplateRoot, path2: ".template.config", path3: "template.json");
        File.Exists(path: configPath).Should().BeTrue(because: "template.json must exist");

        string json = File.ReadAllText(path: configPath);
        JsonDocument doc = JsonDocument.Parse(json: json);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "identity", value: out JsonElement identity).Should().BeTrue();
        identity.GetString().Should().Be(expected: "NoMercy.Plugin.Template");

        root.TryGetProperty(propertyName: "shortName", value: out JsonElement shortName).Should().BeTrue();
        shortName.GetString().Should().Be(expected: "nomercy-plugin");

        root.TryGetProperty(propertyName: "sourceName", value: out JsonElement sourceName).Should().BeTrue();
        sourceName.GetString().Should().Be(expected: "NoMercy.Plugin.Template");

        root.TryGetProperty(propertyName: "symbols", value: out JsonElement symbols).Should().BeTrue();
        symbols
            .TryGetProperty(propertyName: "pluginId", value: out _)
            .Should()
            .BeTrue(because: "template must generate a plugin GUID");
        symbols
            .TryGetProperty(propertyName: "authorName", value: out _)
            .Should()
            .BeTrue(because: "template must accept an author name parameter");
        symbols
            .TryGetProperty(propertyName: "pluginDescription", value: out _)
            .Should()
            .BeTrue(because: "template must accept a description parameter");
    }

    [Fact]
    public void PluginManifest_Exists_AndMatchesSchema()
    {
        string manifestPath = Path.Combine(path1: TemplateRoot, path2: "plugin.json");
        File.Exists(path: manifestPath).Should().BeTrue(because: "plugin.json must exist in template");

        string json = File.ReadAllText(path: manifestPath);
        JsonDocument doc = JsonDocument.Parse(json: json);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "id", value: out _).Should().BeTrue(because: "manifest must have 'id'");
        root.TryGetProperty(propertyName: "name", value: out _).Should().BeTrue(because: "manifest must have 'name'");
        root.TryGetProperty(propertyName: "description", value: out _)
            .Should()
            .BeTrue(because: "manifest must have 'description'");
        root.TryGetProperty(propertyName: "version", value: out _).Should().BeTrue(because: "manifest must have 'version'");
        root.TryGetProperty(propertyName: "assembly", value: out _).Should().BeTrue(because: "manifest must have 'assembly'");

        string version = root.GetProperty(propertyName: "version").GetString()!;
        Version.TryParse(input: version, result: out _).Should().BeTrue(because: "version must be a valid semver string");

        string assembly = root.GetProperty(propertyName: "assembly").GetString()!;
        assembly.Should().EndWith(expected: ".dll", because: "assembly must be a .dll filename");
    }

    [Fact]
    public void PluginManifest_AssemblyName_MatchesCsprojName()
    {
        string manifestPath = Path.Combine(path1: TemplateRoot, path2: "plugin.json");
        string json = File.ReadAllText(path: manifestPath);
        JsonDocument doc = JsonDocument.Parse(json: json);
        string assembly = doc.RootElement.GetProperty(propertyName: "assembly").GetString()!;

        string csprojPath = Path.Combine(path1: TemplateRoot, path2: "NoMercy.Plugin.Template.csproj");
        File.Exists(path: csprojPath).Should().BeTrue(because: "csproj must exist");

        string expectedAssembly = Path.GetFileNameWithoutExtension(path: csprojPath) + ".dll";
        assembly.Should().Be(expected: expectedAssembly, because: "plugin.json assembly must match the csproj name");
    }

    [Fact]
    public void PluginManifest_ContainsPlaceholders()
    {
        string manifestPath = Path.Combine(path1: TemplateRoot, path2: "plugin.json");
        string json = File.ReadAllText(path: manifestPath);

        json.Should()
            .Contain(
                expected: "PLUGIN-GUID-PLACEHOLDER",
                because: "manifest id must use the GUID placeholder for template substitution"
            );
        json.Should()
            .Contain(
                expected: "PLUGIN-DESCRIPTION-PLACEHOLDER",
                because: "manifest description must use the description placeholder"
            );
        json.Should()
            .Contain(expected: "AUTHOR-NAME-PLACEHOLDER", because: "manifest author must use the author placeholder");
    }

    [Fact]
    public void PluginClass_Exists_AndContainsPlaceholders()
    {
        string pluginPath = Path.Combine(path1: TemplateRoot, path2: "Plugin.cs");
        File.Exists(path: pluginPath).Should().BeTrue(because: "Plugin.cs must exist");

        string source = File.ReadAllText(path: pluginPath);
        source.Should().Contain(expected: "IPlugin", because: "Plugin class must implement IPlugin");
        source
            .Should()
            .Contain(expected: "PLUGIN-GUID-PLACEHOLDER", because: "Plugin class must use GUID placeholder");
        source.Should().Contain(expected: "Initialize", because: "Plugin class must implement Initialize method");
        source.Should().Contain(expected: "Dispose", because: "Plugin class must implement Dispose method");
    }

    [Fact]
    public void PluginClass_ImplementsIPluginInterface()
    {
        string pluginPath = Path.Combine(path1: TemplateRoot, path2: "Plugin.cs");
        string source = File.ReadAllText(path: pluginPath);

        source.Should().Contain(expected: "string Name =>", because: "Plugin must have Name property");
        source.Should().Contain(expected: "string Description =>", because: "Plugin must have Description property");
        source.Should().Contain(expected: "Guid Id", because: "Plugin must have Id property");
        source.Should().Contain(expected: "Version Version", because: "Plugin must have Version property");
        source
            .Should()
            .Contain(
                expected: "void Initialize(IPluginContext context)",
                because: "Plugin must have Initialize method"
            );
    }

    [Fact]
    public void Csproj_References_PluginAbstractions()
    {
        string csprojPath = Path.Combine(path1: TemplateRoot, path2: "NoMercy.Plugin.Template.csproj");
        string content = File.ReadAllText(path: csprojPath);

        content
            .Should()
            .Contain(expected: "NoMercy.Plugins.Abstractions", because: "csproj must reference plugin abstractions");
        content.Should().Contain(expected: "net10.0", because: "csproj must target net10.0");
    }

    [Fact]
    public void Csproj_CopiesPluginManifest()
    {
        string csprojPath = Path.Combine(path1: TemplateRoot, path2: "NoMercy.Plugin.Template.csproj");
        string content = File.ReadAllText(path: csprojPath);

        content.Should().Contain(expected: "plugin.json", because: "csproj must include plugin.json");
        content.Should().Contain(expected: "CopyToOutputDirectory", because: "plugin.json must be copied to output");
    }

    [Fact]
    public void TemplatePackageCsproj_Exists()
    {
        string packageCsprojPath = FindRepoPath(
            relativePath: Path.Combine(path1: "templates", path2: "NoMercy.Plugin.Templates.csproj")
        );
        File.Exists(path: packageCsprojPath).Should().BeTrue(because: "Template package csproj must exist");

        string content = File.ReadAllText(path: packageCsprojPath);
        content.Should().Contain(expected: "PackageType>Template", because: "Must be a template package type");
        content.Should().Contain(expected: "NoMercy.Plugin.Template", because: "Must include the template content");
    }

    [Fact]
    public void AllRequiredTemplateFiles_Exist()
    {
        string[] requiredFiles =
        [
            ".template.config/template.json",
            "NoMercy.Plugin.Template.csproj",
            "plugin.json",
            "Plugin.cs",
        ];

        foreach (string file in requiredFiles)
        {
            string fullPath = Path.Combine(path1: TemplateRoot, path2: file);
            File.Exists(path: fullPath).Should().BeTrue(because: $"Required template file '{file}' must exist");
        }
    }

    [Fact]
    public void PluginManifest_HasTargetAbi()
    {
        string manifestPath = Path.Combine(path1: TemplateRoot, path2: "plugin.json");
        string json = File.ReadAllText(path: manifestPath);
        JsonDocument doc = JsonDocument.Parse(json: json);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "targetAbi", value: out JsonElement targetAbi)
            .Should()
            .BeTrue(because: "manifest must have targetAbi");
        targetAbi.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PluginManifest_HasAutoEnabled()
    {
        string manifestPath = Path.Combine(path1: TemplateRoot, path2: "plugin.json");
        string json = File.ReadAllText(path: manifestPath);
        JsonDocument doc = JsonDocument.Parse(json: json);
        JsonElement root = doc.RootElement;

        root.TryGetProperty(propertyName: "autoEnabled", value: out JsonElement autoEnabled)
            .Should()
            .BeTrue(because: "manifest must have autoEnabled");
        autoEnabled.ValueKind.Should().Be(expected: JsonValueKind.True, because: "autoEnabled should default to true");
    }
}
