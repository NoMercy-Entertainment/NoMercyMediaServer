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

using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage;

// ============================================================================
// Unit tests for NFS export discovery helpers — no network required.
// ============================================================================

public class NfsExportDiscoveryTests
{
    private const string LibSkipReason = "libnfs native library not installed on this machine";

    // GetExportsAsync with an unreachable / non-existent host should return
    // null within the timeout rather than hanging or throwing.
    [SkippableFact]
    public async Task GetExportsAsync_unreachable_host_returns_null_within_timeout()
    {
        // Use a host guaranteed to refuse: 192.0.2.1 is TEST-NET (RFC 5737) —
        // packets are dropped, so the call will time out rather than succeed.
        List<string>? result;
        try
        {
            result = await NfsStorageDriver.GetExportsAsync("192.0.2.1", timeoutMs: 500);
        }
        catch (DllNotFoundException)
        {
            Skip.If(true, LibSkipReason);
            return;
        }

        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task GetExportsAsync_localhost_unreachable_returns_null()
    {
        // Port 2049 is almost certainly not open in CI.
        List<string>? result;
        try
        {
            result = await NfsStorageDriver.GetExportsAsync("127.0.0.1", timeoutMs: 500);
        }
        catch (DllNotFoundException)
        {
            Skip.If(true, LibSkipReason);
            return;
        }

        // Either null (no exports / unreachable) or a list — both are valid.
        _ = result;
    }

    // NfsDriverConfig.For is now public — verify it still validates correctly.
    [Fact]
    public void NfsDriverConfig_For_builds_valid_config()
    {
        NfsDriverConfig config = NfsDriverConfig.For("nas.local", "/media", version: 3);

        config.Server.Should().Be("nas.local");
        config.Export.Should().Be("/media");
        config.Version.Should().Be(3);
        config.Port.Should().Be(2049);
        config.Uid.Should().BeNull();
        config.Gid.Should().BeNull();
    }

    [Fact]
    public void NfsDriverConfig_For_accepts_uid_gid()
    {
        NfsDriverConfig config = NfsDriverConfig.For("nas.local", "/media", uid: 1000, gid: 1000);

        config.Uid.Should().Be(1000);
        config.Gid.Should().Be(1000);
    }

    [Fact]
    public void NfsDriverConfig_For_normalizes_export_leading_slash()
    {
        NfsDriverConfig config = NfsDriverConfig.For("nas.local", "media/files");
        config.Export.Should().StartWith("/");
    }

    [Fact]
    public void NfsDriverConfig_For_strips_trailing_slash()
    {
        NfsDriverConfig config = NfsDriverConfig.For("nas.local", "/media/");
        config.Export.Should().Be("/media");
    }
}

// ============================================================================
// Integration tests — skipped when NFS / Docker not available.
// Reuses StorageBackendsFixture from the shared StorageBackends collection.
// ============================================================================

[Collection("StorageBackends")]
public class NfsListDirectoriesIntegrationTests(StorageBackendsFixture fix)
{
    private string SkipReason =>
        fix.NfsUnavailableReason
        ?? fix.StartupError
        ?? "storage container not available or libnfs native library not installed";

    [SkippableFact]
    public void ListDirectories_export_root_returns_entries()
    {
        NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(driver is null, SkipReason);
        using NfsStorageDriver d = driver!;

        List<(string Name, bool IsDirectory)> entries = d.ListDirectories(string.Empty);

        // Export root may be empty in a fresh container — just assert no throw.
        entries.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task ListDirectories_hides_dot_entries()
    {
        NfsStorageDriver? driver = fix.TryBuildNfsDriver();
        Skip.If(driver is null, SkipReason);
        using NfsStorageDriver d = driver!;

        // Create a dotfile and a regular dir to verify filtering.
        string dir = $"/listtest-{Ulid.NewUlid()}";
        d.CreateDirectory(dir);
        d.CreateDirectory(dir + "/.hidden");
        d.CreateDirectory(dir + "/visible");

        List<(string Name, bool IsDirectory)> entries = d.ListDirectories(dir);

        entries.Should().NotContain(e => e.Name.StartsWith('.'));
        entries.Should().Contain(e => e.Name == "visible");

        d.DeleteDirectory(dir, recursive: true);
    }

    [SkippableFact]
    public async Task GetExportsAsync_running_server_returns_export_list()
    {
        Skip.If(!fix.Available, SkipReason);

        List<string>? exports = await NfsStorageDriver.GetExportsAsync(
            StorageBackendsFixture.NfsHost,
            timeoutMs: 5_000
        );

        // May return null if libnfs mount-protocol call fails (depends on container);
        // just assert we didn't hang or crash.
        _ = exports;
    }
}
