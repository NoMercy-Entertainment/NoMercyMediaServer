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

using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Networking.Discovery;
using NoMercy.NmSystem.Auth;
using NoMercy.NmSystem.Status;
using NoMercy.Storage.Drivers.Local;
using NoMercy.Tests.Common;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Tests verifying that the ExternalIp property getter does not block
/// with .Result on an async operation, and that DiscoverExternalIpAsync() eagerly populates the IP.
/// </summary>
[Trait("Category", "Unit")]
public class NetworkingExternalIpTests
{
    [Fact]
    public void ExternalIp_Getter_NoBlockingResult()
    {
        // The ExternalIp getter must NOT call .Result on async GetExternalIp().
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Discovery/NetworkDiscovery.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        bool insideExternalIpGetter = false;
        int braceDepth = 0;
        List<string> getterLines = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.Contains("public string ExternalIp"))
            {
                insideExternalIpGetter = true;
                braceDepth = 0;
                continue;
            }

            if (insideExternalIpGetter)
            {
                if (trimmed.Contains('{'))
                    braceDepth++;
                if (trimmed.Contains('}'))
                    braceDepth--;

                if (trimmed.StartsWith("get"))
                {
                    getterLines.Add(trimmed);
                }

                if (braceDepth <= 0 && getterLines.Count > 0)
                    break;
            }
        }

        Assert.NotEmpty(getterLines);

        foreach (string line in getterLines)
        {
            Assert.DoesNotContain(".Result", line);
        }
    }

    [Fact]
    public void ExternalIp_Getter_ReturnsFallbackWhenNotPopulated()
    {
        // Asserted on the getter's behaviour rather than on the source text it is spelled
        // with. The previous version grepped this line for "??", so it failed the moment the
        // same guarantee was expressed a different way — and it would have passed just as
        // happily if the fallback had been a wrong value.
        NetworkDiscovery discovery = new(
            NullLogger<NetworkDiscovery>.Instance,
            new LocalStorageDriver(),
            new AuthTokenStore(),
            new ConnectivityStatus(),
            new()
        );

        Assert.Equal("0.0.0.0", discovery.ExternalIp);

        discovery.ExternalIp = string.Empty;
        Assert.Equal("0.0.0.0", discovery.ExternalIp);

        discovery.ExternalIp = "203.0.113.42";
        Assert.Equal("203.0.113.42", discovery.ExternalIp);
    }

    [Fact]
    public void Discover_AlwaysPopulatesExternalIp()
    {
        // DiscoverExternalIpAsync() must eagerly fetch the external IP so the getter never blocks.
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Discovery/NetworkDiscovery.cs");
        string source = File.ReadAllText(sourceFile);

        Assert.Contains("string.IsNullOrEmpty(_externalIp)", source);
        Assert.Contains("await GetExternalIpAsync()", source);
    }

    [Fact]
    public void ExternalIp_ReturnsCachedValueWithoutBlocking()
    {
        // After setting ExternalIp, the getter returns the cached value instantly.
        NetworkDiscovery discovery = new(
            NullLogger<NetworkDiscovery>.Instance,
            new LocalStorageDriver(),
            new AuthTokenStore(),
            new ConnectivityStatus(),
            new()
        );
        string original = discovery.ExternalIp;

        discovery.ExternalIp = "1.2.3.4";

        Assert.Equal("1.2.3.4", discovery.ExternalIp);

        // Restore original state
        discovery.ExternalIp = original;
    }

    [Fact]
    public void ExternalIp_DefaultFallbackIsNotEmpty()
    {
        // When _externalIp is null, getter must not return null or empty.
        // We can verify by checking the source — the fallback is "0.0.0.0".
        string sourceFile = FindSourceFile("src/NoMercy.Networking/Discovery/NetworkDiscovery.cs");
        string source = File.ReadAllText(sourceFile);

        string[] lines = source.Split('\n');
        string? getterLine = lines.FirstOrDefault(l =>
            l.Trim().StartsWith("get =>") && l.Contains("externalIp")
        );

        Assert.NotNull(getterLine);
        Assert.Contains("\"0.0.0.0\"", getterLine);
    }

    private static string FindSourceFile(string relativePath)
    {
        return RepoPaths.At(relativePath);
    }
}
