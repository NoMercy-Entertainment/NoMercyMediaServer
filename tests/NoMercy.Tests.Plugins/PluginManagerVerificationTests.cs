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

using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Events;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using NoMercy.Plugins.Verification;
using Xunit;

namespace NoMercy.Tests.Plugins;

public class PluginManagerVerificationTests : IDisposable
{
    private readonly string _tempPluginsDir;
    private readonly PluginManager _manager;

    public PluginManagerVerificationTests()
    {
        _tempPluginsDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-plugin-verify-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempPluginsDir);

        _manager = new(
            eventBus: new InMemoryEventBus(),
            serviceProvider: new MinimalServiceProvider(),
            logger: NullLogger<PluginManager>.Instance,
            pluginsPath: _tempPluginsDir,
            storage: TestStorageHelper.CreateStorage(rootPath: _tempPluginsDir),
            driver: TestStorageHelper.CreateBackend(),
            verifier: new PluginVerifier()
        );
    }

    public void Dispose()
    {
        _manager.Dispose();

        try
        {
            if (Directory.Exists(path: _tempPluginsDir))
            {
                Directory.Delete(path: _tempPluginsDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup
        }
    }

    private string WriteSourceDll(byte[] bytes)
    {
        string path = Path.Combine(path1: _tempPluginsDir, path2: $"source-{Guid.NewGuid():N}.dll");
        File.WriteAllBytes(path: path, bytes: bytes);
        return path;
    }

    private string InstalledPathFor(string sourceDll)
    {
        string pluginName = Path.GetFileNameWithoutExtension(path: sourceDll);
        return Path.Combine(path1: _tempPluginsDir, path2: pluginName, path3: Path.GetFileName(path: sourceDll));
    }

    [Fact]
    public async Task InstallPluginAsync_ChecksumMismatch_ThrowsAndDoesNotCopy()
    {
        string sourceDll = WriteSourceDll(bytes: [9, 9, 9]);

        Func<Task> act = () => _manager.InstallPluginAsync(packagePath: sourceDll, expectedChecksum: "deadbeef");

        await act.Should().ThrowAsync<PluginVerificationException>();
        File.Exists(path: InstalledPathFor(sourceDll: sourceDll)).Should().BeFalse();
    }

    [Fact]
    public async Task InstallPluginAsync_ChecksumMatch_CopiesAssembly()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        string sourceDll = WriteSourceDll(bytes: bytes);
        string sha = Convert.ToHexString(inArray: SHA256.HashData(source: bytes)).ToLowerInvariant();

        await _manager.InstallPluginAsync(packagePath: sourceDll, expectedChecksum: sha);

        File.Exists(path: InstalledPathFor(sourceDll: sourceDll)).Should().BeTrue();
    }

    [Fact]
    public async Task InstallPluginAsync_NoExpectedChecksum_SkipsVerificationAndCopies()
    {
        string sourceDll = WriteSourceDll(bytes: [7, 7, 7]);

        await _manager.InstallPluginAsync(packagePath: sourceDll);

        File.Exists(path: InstalledPathFor(sourceDll: sourceDll)).Should().BeTrue();
    }

    [Fact]
    public async Task LoadPluginFromManifestAsync_AbiMismatch_MarksMalfunctionedInsteadOfInstantiating()
    {
        Guid pluginId = Guid.NewGuid();
        string pluginDir = Path.Combine(path1: _tempPluginsDir, path2: "AbiMismatchPlugin");
        Directory.CreateDirectory(path: pluginDir);

        string dllPath = Path.Combine(path1: pluginDir, path2: "AbiMismatchPlugin.dll");
        await File.WriteAllBytesAsync(path: dllPath, bytes: [1, 2, 3]);

        string manifestJson =
            $@"{{
            ""id"": ""{pluginId}"",
            ""name"": ""AbiMismatchPlugin"",
            ""description"": ""A test"",
            ""version"": ""1.0.0"",
            ""assembly"": ""AbiMismatchPlugin.dll"",
            ""targetAbi"": ""11.0""
        }}";
        string manifestPath = Path.Combine(path1: pluginDir, path2: "plugin.json");
        await File.WriteAllTextAsync(path: manifestPath, contents: manifestJson);

        await _manager.LoadPluginFromManifestAsync(manifestPath: manifestPath);

        PluginInfo? info = _manager.GetInstalledPlugins().FirstOrDefault(predicate: p => p.Id == pluginId);

        info.Should().NotBeNull();
        info!.Status.Should().Be(expected: PluginStatus.Malfunctioned);
        info.Verified.Should().BeFalse();
        _manager.GetPluginInstance(pluginId: pluginId).Should().BeNull();
    }

    private sealed class MinimalServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
