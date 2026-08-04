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

using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManifestParserV2Tests
{
    private const string V1Json = """
        { "id":"5KTKRT4Z2Y9P59Y40W5CX4TQKF","name":"Internet Radio Provider",
          "description":"radio","version":"1.0.0","targetAbi":"10.0",
          "assembly":"NoMercy.Plugin.InternetRadio.dll","autoEnabled":true }
        """;

    [Fact]
    public void Parse_V1Manifest_StillValidatesAndHasNullCapabilities()
    {
        PluginManifest manifest = PluginManifestParser.Parse(V1Json);
        Assert.Equal("Internet Radio Provider", manifest.Name);
        Assert.Null(manifest.Capabilities);
        Assert.Null(manifest.Signature);
    }

    [Fact]
    public void Parse_V2Manifest_PopulatesCapabilities()
    {
        string json = """
            { "id":"5KTKRT4Z2Y9P59Y40W5CX4TQKF","name":"Radio","description":"d",
              "version":"1.0.0","assembly":"x.dll",
              "capabilities":{ "hooks":["mediaSource","ui"],
                "network":{"hosts":["*.somafm.com"]},
                "ui":{"mounts":[{"section":"music","label":"Radio","route":"/"}]},
                "rest":true,"ws":false } }
            """;
        PluginManifest manifest = PluginManifestParser.Parse(json);
        Assert.NotNull(manifest.Capabilities);
        Assert.Contains("mediaSource", manifest.Capabilities!.Hooks);
        Assert.Equal("*.somafm.com", manifest.Capabilities.Network!.Hosts[0]);
        Assert.Equal("music", manifest.Capabilities.Ui!.Mounts[0].Section);
        Assert.True(manifest.Capabilities.Rest);
    }
}
