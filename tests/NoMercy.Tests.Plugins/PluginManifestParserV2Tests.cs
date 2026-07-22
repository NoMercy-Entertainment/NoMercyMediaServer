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
        { "id":"b3d4f1a2-7c5e-4d8a-9f10-1c2b3a4d5e6f","name":"Internet Radio Provider",
          "description":"radio","version":"1.0.0","targetAbi":"10.0",
          "assembly":"NoMercy.Plugin.InternetRadio.dll","autoEnabled":true }
        """;

    [Fact]
    public void Parse_V1Manifest_StillValidatesAndHasNullCapabilities()
    {
        PluginManifest manifest = PluginManifestParser.Parse(json: V1Json);
        Assert.Equal(expected: "Internet Radio Provider", actual: manifest.Name);
        Assert.Null(@object: manifest.Capabilities);
        Assert.Null(@object: manifest.Signature);
    }

    [Fact]
    public void Parse_V2Manifest_PopulatesCapabilities()
    {
        string json = """
            { "id":"b3d4f1a2-7c5e-4d8a-9f10-1c2b3a4d5e6f","name":"Radio","description":"d",
              "version":"1.0.0","assembly":"x.dll",
              "capabilities":{ "hooks":["mediaSource","ui"],
                "network":{"hosts":["*.somafm.com"]},
                "ui":{"mounts":[{"section":"music","label":"Radio","route":"/"}]},
                "rest":true,"ws":false } }
            """;
        PluginManifest manifest = PluginManifestParser.Parse(json: json);
        Assert.NotNull(@object: manifest.Capabilities);
        Assert.Contains(expected: "mediaSource", collection: manifest.Capabilities!.Hooks);
        Assert.Equal(expected: "*.somafm.com", actual: manifest.Capabilities.Network!.Hosts[index: 0]);
        Assert.Equal(expected: "music", actual: manifest.Capabilities.Ui!.Mounts[index: 0].Section);
        Assert.True(condition: manifest.Capabilities.Rest);
    }
}
