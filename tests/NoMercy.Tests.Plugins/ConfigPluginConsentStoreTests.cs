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

using FluentAssertions;
using NoMercy.Plugins;
using NoMercy.Plugins.Capabilities;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// Backs <see cref="ConfigPluginConsentStore"/> with a real
/// <see cref="PluginConfiguration"/> over a real temp-directory-scoped
/// <see cref="NoMercy.Storage.LocalStorage"/> — no mocking of either
/// collaborator — so persistence-across-instances behavior (the store's whole
/// reason for existing over an in-memory HashSet) is genuinely exercised.
/// </summary>
public class ConfigPluginConsentStoreTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigPluginConsentStoreTests()
    {
        _tempDir = Path.Combine(
            path1: Path.GetTempPath(),
            path2: "nomercy-consent-store-tests-" + Guid.NewGuid().ToString(format: "N")
        );
        Directory.CreateDirectory(path: _tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(path: _tempDir))
                Directory.Delete(path: _tempDir, recursive: true);
        }
        catch (IOException) { }
    }

    private ConfigPluginConsentStore MakeStore() =>
        new(configuration: new PluginConfiguration(dataFolderPath: _tempDir, storage: TestStorageHelper.CreateStorage(rootPath: _tempDir)));

    [Fact]
    public void Contains_NoConfigFileYet_ReturnsFalse()
    {
        ConfigPluginConsentStore store = MakeStore();

        store.Contains(pluginId: Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Add_ThenContains_ReturnsTrue()
    {
        ConfigPluginConsentStore store = MakeStore();
        Guid id = Guid.NewGuid();

        store.Add(pluginId: id);

        store.Contains(pluginId: id).Should().BeTrue();
    }

    [Fact]
    public void Add_PersistsAcrossNewStoreInstances()
    {
        // The whole point of a config-backed store over an in-memory HashSet:
        // a second store instance reading the SAME config file must observe
        // the grant a completely different store instance made.
        Guid id = Guid.NewGuid();
        MakeStore().Add(pluginId: id);

        ConfigPluginConsentStore secondInstance = MakeStore();

        secondInstance.Contains(pluginId: id).Should().BeTrue();
    }

    [Fact]
    public void Add_SameIdTwice_DoesNotDuplicateOrThrow()
    {
        ConfigPluginConsentStore store = MakeStore();
        Guid id = Guid.NewGuid();

        store.Add(pluginId: id);
        Action act = () => store.Add(pluginId: id);

        act.Should().NotThrow();
        store.Contains(pluginId: id).Should().BeTrue();
    }

    [Fact]
    public void Add_SecondDifferentId_BothPersist()
    {
        ConfigPluginConsentStore store = MakeStore();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        store.Add(pluginId: first);
        store.Add(pluginId: second);

        store.Contains(pluginId: first).Should().BeTrue();
        store.Contains(pluginId: second).Should().BeTrue();
    }

    [Fact]
    public void Remove_NoConfigFileYet_DoesNotThrow()
    {
        ConfigPluginConsentStore store = MakeStore();

        Action act = () => store.Remove(pluginId: Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Remove_IdNotGranted_DoesNotThrowAndLeavesOthersIntact()
    {
        ConfigPluginConsentStore store = MakeStore();
        Guid granted = Guid.NewGuid();
        store.Add(pluginId: granted);

        Action act = () => store.Remove(pluginId: Guid.NewGuid());

        act.Should().NotThrow();
        store.Contains(pluginId: granted).Should().BeTrue();
    }

    [Fact]
    public void Remove_GrantedId_RemovesIt()
    {
        ConfigPluginConsentStore store = MakeStore();
        Guid id = Guid.NewGuid();
        store.Add(pluginId: id);

        store.Remove(pluginId: id);

        store.Contains(pluginId: id).Should().BeFalse();
    }

    [Fact]
    public void Remove_PersistsAcrossNewStoreInstances()
    {
        Guid id = Guid.NewGuid();
        ConfigPluginConsentStore first = MakeStore();
        first.Add(pluginId: id);
        first.Remove(pluginId: id);

        ConfigPluginConsentStore second = MakeStore();

        second.Contains(pluginId: id).Should().BeFalse();
    }
}
