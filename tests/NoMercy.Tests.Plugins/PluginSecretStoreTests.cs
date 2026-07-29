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
using Microsoft.AspNetCore.DataProtection;
using NoMercy.Plugins;
using NoMercy.Plugins.Abstractions;
using Xunit;

namespace NoMercy.Tests.Plugins;

/// <summary>
/// The store exists so a plugin author cannot accidentally write a password in
/// the clear, so the tests that matter are: is the stored form actually
/// protected, and can one plugin read another's secret.
/// </summary>
public class PluginSecretStoreTests
{
    private static readonly Guid PluginA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PluginB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PluginSecretStore Store(Guid pluginId, IPluginConfiguration configuration) =>
        new(pluginId, new EphemeralDataProtectionProvider(), configuration);

    [Fact]
    public async Task A_value_round_trips()
    {
        PluginSecretStore store = Store(PluginA, new InMemoryPluginConfiguration());
        await store.SetAsync("api-key", "hunter2");

        (await store.GetAsync("api-key")).Should().Be("hunter2");
    }

    [Fact]
    public async Task A_key_that_was_never_set_reads_null()
    {
        PluginSecretStore store = Store(PluginA, new InMemoryPluginConfiguration());

        (await store.GetAsync("nothing-here")).Should().BeNull();
    }

    [Fact]
    public async Task What_lands_in_storage_is_not_the_secret()
    {
        // The whole point. If this fails the store is worse than useless,
        // because an author would trust it.
        InMemoryPluginConfiguration configuration = new();
        PluginSecretStore store = Store(PluginA, configuration);

        await store.SetAsync("password", "correct-horse-battery-staple");

        PluginSecretRecord? record = configuration.GetConfiguration<PluginSecretRecord>();
        record.Should().NotBeNull();
        record!
            .Values.Values.Should()
            .NotContain(stored => stored.Contains("correct-horse-battery-staple"));
    }

    [Fact]
    public async Task One_plugin_cannot_read_anothers_secret_through_the_same_store()
    {
        // Same backing configuration, two plugins. The key is namespaced by
        // plugin id, so B asking for A's key name gets nothing.
        InMemoryPluginConfiguration shared = new();

        await Store(PluginA, shared).SetAsync("token", "a-secret");

        (await Store(PluginB, shared).GetAsync("token")).Should().BeNull();
    }

    [Fact]
    public async Task A_value_protected_by_another_plugin_does_not_unprotect()
    {
        // Defence in depth behind the key namespace: even reaching the stored
        // bytes, the protector's purpose string differs per plugin.
        InMemoryPluginConfiguration shared = new();
        IDataProtectionProvider provider = new EphemeralDataProtectionProvider();

        PluginSecretStore storeA = new(PluginA, provider, shared);
        await storeA.SetAsync("token", "a-secret");

        PluginSecretRecord record = shared.GetConfiguration<PluginSecretRecord>()!;
        string protectedValue = record.Values.Values.Single();

        // Re-file A's protected bytes under B's key and try to read them as B.
        record.Values[$"{PluginB:D}:token"] = protectedValue;
        shared.SaveConfiguration(record);

        PluginSecretStore storeB = new(PluginB, provider, shared);
        (await storeB.GetAsync("token")).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_removes_the_value()
    {
        PluginSecretStore store = Store(PluginA, new InMemoryPluginConfiguration());
        await store.SetAsync("api-key", "hunter2");
        await store.DeleteAsync("api-key");

        (await store.GetAsync("api-key")).Should().BeNull();
    }

    [Fact]
    public async Task Deleting_something_absent_is_not_an_error()
    {
        PluginSecretStore store = Store(PluginA, new InMemoryPluginConfiguration());

        Func<Task> act = () => store.DeleteAsync("never-existed");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Keys_lists_only_this_plugins_key_names()
    {
        InMemoryPluginConfiguration shared = new();
        await Store(PluginA, shared).SetAsync("a-key", "x");
        await Store(PluginB, shared).SetAsync("b-key", "y");

        IReadOnlyList<string> keys = await Store(PluginA, shared).KeysAsync();

        keys.Should().BeEquivalentTo(["a-key"]);
    }

    [Fact]
    public async Task A_value_from_a_different_key_ring_reads_null_rather_than_throwing()
    {
        // A restored backup or a rotated key leaves values that will not
        // unprotect. A plugin must not fail to start over it.
        InMemoryPluginConfiguration shared = new();
        await Store(PluginA, shared).SetAsync("token", "a-secret");

        PluginSecretStore withDifferentKeys = Store(PluginA, shared);

        (await withDifferentKeys.GetAsync("token")).Should().BeNull();
    }

    [Fact]
    public async Task An_empty_key_is_refused()
    {
        PluginSecretStore store = Store(PluginA, new InMemoryPluginConfiguration());

        Func<Task> act = () => store.SetAsync("  ", "value");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
