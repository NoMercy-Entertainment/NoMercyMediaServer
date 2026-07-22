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
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// Tests verifying that the ExternalIp property getter does not block
/// with .Result on an async operation, and that DiscoverExternalIpAsync() eagerly populates the IP.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class NetworkingExternalIpTests
{
    [Fact]
    public void ExternalIp_Getter_NoBlockingResult()
    {
        // The ExternalIp getter must NOT call .Result on async GetExternalIp().
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Discovery/NetworkDiscovery.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        bool insideExternalIpGetter = false;
        int braceDepth = 0;
        List<string> getterLines = [];

        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.Contains(value: "public string ExternalIp"))
            {
                insideExternalIpGetter = true;
                braceDepth = 0;
                continue;
            }

            if (insideExternalIpGetter)
            {
                if (trimmed.Contains(value: '{'))
                    braceDepth++;
                if (trimmed.Contains(value: '}'))
                    braceDepth--;

                if (trimmed.StartsWith(value: "get"))
                {
                    getterLines.Add(item: trimmed);
                }

                if (braceDepth <= 0 && getterLines.Count > 0)
                    break;
            }
        }

        Assert.NotEmpty(collection: getterLines);

        foreach (string line in getterLines)
        {
            Assert.DoesNotContain(expectedSubstring: ".Result", actualString: line);
        }
    }

    [Fact]
    public void ExternalIp_Getter_ReturnsFallbackWhenNotPopulated()
    {
        // The getter should return a safe fallback ("0.0.0.0"), not call async methods.
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Discovery/NetworkDiscovery.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        string? getterLine = lines.FirstOrDefault(predicate: l =>
            l.Trim().StartsWith(value: "get =>") && l.Contains(value: "externalIp")
        );

        Assert.NotNull(@object: getterLine);
        Assert.Contains(expectedSubstring: "??", actualString: getterLine);
        Assert.DoesNotContain(expectedSubstring: "GetExternalIp()", actualString: getterLine);
    }

    [Fact]
    public void Discover_AlwaysPopulatesExternalIp()
    {
        // DiscoverExternalIpAsync() must eagerly fetch the external IP so the getter never blocks.
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Discovery/NetworkDiscovery.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        Assert.Contains(expectedSubstring: "string.IsNullOrEmpty", actualString: source);
        Assert.Contains(expectedSubstring: "await GetExternalIpAsync()", actualString: source);
    }

    [Fact]
    public void ExternalIp_ReturnsCachedValueWithoutBlocking()
    {
        // After setting ExternalIp, the getter returns the cached value instantly.
        NetworkDiscovery discovery = new(
            logger: NullLogger<NetworkDiscovery>.Instance,
            driver: new LocalStorageDriver(),
            authTokenStore: new AuthTokenStore(),
            connectivityStatus: new ConnectivityStatus(),
            networkProbeConfig: new()
        );
        string original = discovery.ExternalIp;

        discovery.ExternalIp = "1.2.3.4";

        Assert.Equal(expected: "1.2.3.4", actual: discovery.ExternalIp);

        // Restore original state
        discovery.ExternalIp = original;
    }

    [Fact]
    public void ExternalIp_DefaultFallbackIsNotEmpty()
    {
        // When _externalIp is null, getter must not return null or empty.
        // We can verify by checking the source — the fallback is "0.0.0.0".
        string sourceFile = FindSourceFile(
            relativePath: "src/NoMercy.Networking/Discovery/NetworkDiscovery.cs"
        );
        string source = File.ReadAllText(path: sourceFile);

        string[] lines = source.Split(separator: '\n');
        string? getterLine = lines.FirstOrDefault(predicate: l =>
            l.Trim().StartsWith(value: "get =>") && l.Contains(value: "externalIp")
        );

        Assert.NotNull(@object: getterLine);
        Assert.Contains(expectedSubstring: "\"0.0.0.0\"", actualString: getterLine);
    }

    private static string FindSourceFile(string relativePath)
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            string candidate = Path.Combine(path1: dir, path2: relativePath);
            if (File.Exists(path: candidate))
                return candidate;

            string repoCandidate = Path.Combine(
                paths: [dir, "..", "..", "..", "..", "..", relativePath]
            );
            string resolved = Path.GetFullPath(path: repoCandidate);
            if (File.Exists(path: resolved))
                return resolved;

            dir = Directory.GetParent(path: dir)?.FullName;
        }

        string fallback = Path.Combine(
            path1: "/workspaces/NoMercyMediaServer",
            path2: relativePath
        );
        if (File.Exists(path: fallback))
            return fallback;

        throw new FileNotFoundException(message: $"Could not find source file: {relativePath}");
    }
}
