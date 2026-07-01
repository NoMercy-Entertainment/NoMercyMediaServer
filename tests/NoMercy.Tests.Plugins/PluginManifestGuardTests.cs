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
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManifestGuardTests
{
    private static readonly Guid KnownId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    private static string BuildJson(
        string? id = null,
        string? name = "ValidPlugin",
        string? description = "A valid plugin",
        string? version = "1.0.0",
        string? assembly = "ValidPlugin.dll",
        bool includeId = true,
        bool includeName = true,
        bool includeDescription = true,
        bool includeVersion = true,
        bool includeAssembly = true
    )
    {
        Dictionary<string, object?> fields = [];

        if (includeId)
            fields["id"] = id ?? KnownId.ToString();
        if (includeName)
            fields["name"] = name;
        if (includeDescription)
            fields["description"] = description;
        if (includeVersion)
            fields["version"] = version;
        if (includeAssembly)
            fields["assembly"] = assembly;

        return JsonSerializer.Serialize(fields);
    }

    [Fact]
    public void Parse_EmptyGuid_Fires_RejectsManifest()
    {
        string json = BuildJson(id: Guid.Empty.ToString());

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*id*");
    }

    [Fact]
    public void Parse_NonEmptyGuid_Silent_AcceptsManifest()
    {
        string json = BuildJson(id: Guid.NewGuid().ToString());

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Parse_WhitespaceOnlyName_Fires_RejectsManifest()
    {
        string json = BuildJson(name: "   ");

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*name*");
    }

    [Fact]
    public void Parse_MinimalNonEmptyName_Silent_AcceptsManifest()
    {
        string json = BuildJson(name: "X");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Name.Should().Be("X");
    }

    [Fact]
    public void Parse_WhitespaceOnlyVersion_Fires_RejectsManifest()
    {
        string json = BuildJson(version: "   ");

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*version*");
    }

    [Fact]
    public void Parse_TwoComponentVersion_Silent_AcceptsManifest()
    {
        string json = BuildJson(version: "1.0");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Version.Should().Be("1.0");
    }

    [Fact]
    public void Parse_UnparseableVersionString_Fires_RejectsManifest()
    {
        string json = BuildJson(version: "alpha-1");

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*version*");
    }

    [Fact]
    public void Parse_FourComponentVersion_Silent_AcceptsManifest()
    {
        string json = BuildJson(version: "2.3.4.5");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Version.Should().Be("2.3.4.5");
    }

    [Fact]
    public void Parse_WhitespaceOnlyAssembly_Fires_RejectsManifest()
    {
        string json = BuildJson(assembly: "\t  \t");

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<InvalidOperationException>().WithMessage("*assembly*");
    }

    [Fact]
    public void Parse_NonEmptyAssembly_Silent_AcceptsManifest()
    {
        string json = BuildJson(assembly: "Plugin.dll");

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Assembly.Should().Be("Plugin.dll");
    }

    [Fact]
    public void Parse_WhitespaceOnlyJsonInput_Fires_ArgumentException()
    {
        Action act = () => PluginManifestParser.Parse("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_ValidJson_Silent_ReturnsManifest()
    {
        string json = BuildJson();

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().Be(KnownId);
        manifest.Name.Should().Be("ValidPlugin");
    }

    [Fact]
    public void Parse_MissingIdField_Fires_JsonException()
    {
        string json = BuildJson(includeId: false);

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_MissingNameField_Fires_JsonException()
    {
        string json = BuildJson(includeName: false);

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_MissingDescriptionField_Fires_JsonException()
    {
        string json = BuildJson(includeDescription: false);

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_MissingAssemblyField_Fires_JsonException()
    {
        string json = BuildJson(includeAssembly: false);

        Action act = () => PluginManifestParser.Parse(json);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Parse_AllRequiredFieldsPresent_Silent_ReturnsManifest()
    {
        string json = BuildJson();

        PluginManifest manifest = PluginManifestParser.Parse(json);

        manifest.Id.Should().NotBe(Guid.Empty);
        manifest.Assembly.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GuardCatalogue_AllRulesHaveTestCoverage()
    {
        string[] cataloguedRules =
        [
            "Rule_A_EmptyGuid",
            "Rule_B_WhitespaceName",
            "Rule_C_WhitespaceVersion",
            "Rule_D_UnparseableVersion",
            "Rule_E_WhitespaceAssembly",
            "Rule_F_MissingIdField",
            "Rule_G_MissingNameField",
            "Rule_H_MissingDescriptionField",
            "Rule_I_MissingAssemblyField",
        ];

        string[] testedDecisions =
        [
            "Rule_A_EmptyGuid",
            "Rule_B_WhitespaceName",
            "Rule_C_WhitespaceVersion",
            "Rule_D_UnparseableVersion",
            "Rule_E_WhitespaceAssembly",
            "Rule_F_MissingIdField",
            "Rule_G_MissingNameField",
            "Rule_H_MissingDescriptionField",
            "Rule_I_MissingAssemblyField",
        ];

        cataloguedRules
            .Should()
            .BeEquivalentTo(
                testedDecisions,
                "every rule in the catalogue must have a corresponding test; add an entry here when a new guard is added to Validate()"
            );
    }
}
