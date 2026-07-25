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
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.Status;
using NoMercy.Storage;
using NoMercy.Tests.Networking.Infrastructure;
using Xunit;

namespace NoMercy.Tests.Networking;

/// <summary>
/// REQUIREMENT: GetExternalIpAsync's fallback chain is API → UPnP → file
/// cache → empty string. With no auth token (API skipped) and no UPnP device
/// discovered (never reachable without real hardware), the cache is the last
/// real decision point before giving up — it must return the cached value
/// when present, and the empty-string sentinel (never null, never throw) when
/// absent. These exercise the real method via a fake IStorageDriver so no
/// live network call and no real filesystem write ever happens.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NetworkDiscoveryExternalIpFallbackChainTests
{
    private static string CacheFilePath => Path.Combine(AppFiles.ConfigPath, "external_ip.cache");

    private static NetworkDiscovery BuildDiscovery(IStorageDriver driver)
    {
        return new(
            NullLogger<NetworkDiscovery>.Instance,
            driver,
            new AuthTokenStore(), // AccessToken null — the live API call is skipped
            new ConnectivityStatus(),
            new()
        );
    }

    [Fact]
    public async Task GetExternalIpAsync_NoToken_NoDevice_NoCache_ReturnsEmptyString()
    {
        InMemoryStorageDriverStub driver = new();
        NetworkDiscovery discovery = BuildDiscovery(driver);

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetExternalIpAsync_NoToken_NoDevice_CachePresent_ReturnsCachedValue()
    {
        InMemoryStorageDriverStub driver = new();
        using (Stream write = driver.OpenWrite(CacheFilePath, true))
        await using (StreamWriter writer = new(write, leaveOpen: true))
            writer.Write("198.51.100.7");

        NetworkDiscovery discovery = BuildDiscovery(driver);

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal("198.51.100.7", result);
    }

    [Fact]
    public async Task GetExternalIpAsync_CacheContainsOnlyWhitespace_TreatedAsMiss()
    {
        InMemoryStorageDriverStub driver = new();
        using (Stream write = driver.OpenWrite(CacheFilePath, true))
        await using (StreamWriter writer = new(write, leaveOpen: true))
            writer.Write("   ");

        NetworkDiscovery discovery = BuildDiscovery(driver);

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetExternalIpAsync_CacheValueIsTrimmed()
    {
        InMemoryStorageDriverStub driver = new();
        using (Stream write = driver.OpenWrite(CacheFilePath, true))
        await using (StreamWriter writer = new(write, leaveOpen: true))
            writer.Write("  203.0.113.9  \n");

        NetworkDiscovery discovery = BuildDiscovery(driver);

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal("203.0.113.9", result);
    }

    [Fact]
    public async Task CacheExternalIp_ThenGetExternalIpAsync_RoundTripsThroughLoadCachedExternalIp()
    {
        InMemoryStorageDriverStub driver = new();
        NetworkDiscovery discovery = BuildDiscovery(driver);

        discovery.CacheExternalIp("203.0.113.200");
        string result = await discovery.GetExternalIpAsync();

        Assert.Equal("203.0.113.200", result);
    }

    [Fact]
    public void CacheExternalIp_Overwrite_ReplacesPreviousValue()
    {
        InMemoryStorageDriverStub driver = new();
        NetworkDiscovery discovery = BuildDiscovery(driver);

        discovery.CacheExternalIp("1.1.1.1");
        discovery.CacheExternalIp("2.2.2.2");

        Assert.True(driver.FileExists(CacheFilePath));
        using StreamReader reader = new(driver.OpenRead(CacheFilePath));
        Assert.Equal("2.2.2.2", reader.ReadToEnd());
    }

    /// <summary>
    /// Simulates a cache file that exists but can no longer be read (disk
    /// error, permissions change mid-flight) — proves LoadCachedExternalIp's
    /// catch-and-return-null is real error handling, not dead code.
    /// </summary>
    private sealed class ThrowsOnReadStorageDriverStub : IStorageDriver
    {
        public bool FileExists(string path) => true;

        public Stream OpenRead(string path) => throw new IOException("disk error");

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public void CreateDirectory(string path) => throw new NotSupportedException();

        public void DeleteFile(string path) => throw new NotSupportedException();

        public void DeleteDirectory(string path, bool recursive) =>
            throw new NotSupportedException();

        public long GetFileSize(string path) => throw new NotSupportedException();

        public DateTime GetLastWriteTimeUtc(string path) => throw new NotSupportedException();

        public DateTime GetCreationTimeUtc(string path) => throw new NotSupportedException();

        public DateTime GetLastAccessTimeUtc(string path) => throw new NotSupportedException();

        public Stream OpenWrite(string path, bool overwrite) => throw new NotSupportedException();

        public void MoveFile(string source, string destination) =>
            throw new NotSupportedException();

        public void CopyFile(string source, string destination, bool overwrite) =>
            throw new NotSupportedException();

        public IEnumerable<string> EnumerateFileSystemEntries(
            string directory,
            string searchPattern,
            SearchOption option
        ) => throw new NotSupportedException();

        public string GetFullPath(string path) => path;

        public string? ResolveLinkTarget(string path) => null;

        public bool IsHidden(string path) => false;

        public void MoveDirectory(string source, string destination) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task GetExternalIpAsync_CacheReadThrows_IsSwallowed_ReturnsEmptyString()
    {
        NetworkDiscovery discovery = BuildDiscovery(new ThrowsOnReadStorageDriverStub());

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task GetExternalIpAsync_EmptyAccessToken_AlsoSkipsApiCall_FallsBackToCache()
    {
        InMemoryStorageDriverStub driver = new();
        using (Stream write = driver.OpenWrite(CacheFilePath, true))
        await using (StreamWriter writer = new(write, leaveOpen: true))
            writer.Write("192.0.2.55");

        AuthTokenStore tokenStore = new();
        tokenStore.SetAccessToken(string.Empty);
        NetworkDiscovery discovery = new(
            NullLogger<NetworkDiscovery>.Instance,
            driver,
            tokenStore,
            new ConnectivityStatus(),
            new()
        );

        string result = await discovery.GetExternalIpAsync();

        Assert.Equal("192.0.2.55", result);
    }
}
