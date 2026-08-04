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
            Path.GetTempPath(),
            "nomercy-consent-store-tests-" + Ulid.NewUlid().ToString()
        );
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
    }

    private ConfigPluginConsentStore MakeStore() =>
        new(new PluginConfiguration(_tempDir, TestStorageHelper.CreateStorage(_tempDir)));

    [Fact]
    public void Contains_NoConfigFileYet_ReturnsFalse()
    {
        ConfigPluginConsentStore store = MakeStore();

        store.Contains(Ulid.NewUlid()).Should().BeFalse();
    }

    [Fact]
    public void Add_ThenContains_ReturnsTrue()
    {
        ConfigPluginConsentStore store = MakeStore();
        Ulid id = Ulid.NewUlid();

        store.Add(id);

        store.Contains(id).Should().BeTrue();
    }

    [Fact]
    public void Add_PersistsAcrossNewStoreInstances()
    {
        // The whole point of a config-backed store over an in-memory HashSet:
        // a second store instance reading the SAME config file must observe
        // the grant a completely different store instance made.
        Ulid id = Ulid.NewUlid();
        MakeStore().Add(id);

        ConfigPluginConsentStore secondInstance = MakeStore();

        secondInstance.Contains(id).Should().BeTrue();
    }

    [Fact]
    public void Add_SameIdTwice_DoesNotDuplicateOrThrow()
    {
        ConfigPluginConsentStore store = MakeStore();
        Ulid id = Ulid.NewUlid();

        store.Add(id);
        Action act = () => store.Add(id);

        act.Should().NotThrow();
        store.Contains(id).Should().BeTrue();
    }

    [Fact]
    public void Add_SecondDifferentId_BothPersist()
    {
        ConfigPluginConsentStore store = MakeStore();
        Ulid first = Ulid.NewUlid();
        Ulid second = Ulid.NewUlid();

        store.Add(first);
        store.Add(second);

        store.Contains(first).Should().BeTrue();
        store.Contains(second).Should().BeTrue();
    }

    [Fact]
    public void Remove_NoConfigFileYet_DoesNotThrow()
    {
        ConfigPluginConsentStore store = MakeStore();

        Action act = () => store.Remove(Ulid.NewUlid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Remove_IdNotGranted_DoesNotThrowAndLeavesOthersIntact()
    {
        ConfigPluginConsentStore store = MakeStore();
        Ulid granted = Ulid.NewUlid();
        store.Add(granted);

        Action act = () => store.Remove(Ulid.NewUlid());

        act.Should().NotThrow();
        store.Contains(granted).Should().BeTrue();
    }

    [Fact]
    public void Remove_GrantedId_RemovesIt()
    {
        ConfigPluginConsentStore store = MakeStore();
        Ulid id = Ulid.NewUlid();
        store.Add(id);

        store.Remove(id);

        store.Contains(id).Should().BeFalse();
    }

    [Fact]
    public void Remove_PersistsAcrossNewStoreInstances()
    {
        Ulid id = Ulid.NewUlid();
        ConfigPluginConsentStore first = MakeStore();
        first.Add(id);
        first.Remove(id);

        ConfigPluginConsentStore second = MakeStore();

        second.Contains(id).Should().BeFalse();
    }
}
