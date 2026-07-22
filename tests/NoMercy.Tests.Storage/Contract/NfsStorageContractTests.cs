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
using NoMercy.Storage.Drivers.Nfs;
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Faults;

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Contract test driver for <see cref="RemoteStorage"/> backed by
/// <see cref="NfsStorageDriver"/> using the in-memory <see cref="FaultyLibNfs"/> fake.
///
/// Known divergences from the contract (all are documented failures, not test bugs):
///
///   NFSC-1 — ReadDir not implemented in FaultyLibNfs.
///     FaultyLibNfs.ReadDir() always returns IntPtr.Zero. CollectEntries() in
///     NfsStorageDriver immediately breaks out of the read loop, returning an empty
///     list. Tests that rely on List returning non-empty entries are skipped.
///     Fix target: implement ReadDir in FaultyLibNfs to serve from the in-memory
///     file map, or switch contract List tests to a higher-fidelity fake.
///
///   NFSC-2 — LastModified returns epoch for FaultyLibNfs entries.
///     FaultyLibNfs.Stat64 returns a zero-initialized NfsStat64, so MtimeSec=0.
///     LastModifiedAsync returns DateTimeOffset.UnixEpoch (1970-01-01), not a
///     "recent" timestamp. The "last-modified is recent" contract test is skipped.
///     Fix target: FaultyLibNfs.Stat64 should set MtimeSec to DateTimeOffset.UtcNow.ToUnixTimeSeconds().
///
///   NFSC-3 — CLOSED. RemoteStorage.V() now calls StoragePathGuard.StructuralValidate
///     which rejects null bytes, ".." traversal, device paths, and OS-absolute /
///     backend-absolute paths before the driver sees them.
///
///   NFSC-4 — Empty-string root existence.
///     RemoteStorage.ExistsAsync("") calls NfsStorageDriver.FileExists("") and
///     DirectoryExists(""). ToNfsPath("") produces "/". FaultyLibNfs.Stat64("/") is
///     seeded by EnsureParentDirs (via Seed), but only when at least one Seed() call
///     has been made. On a fresh empty fake the root "/" is not guaranteed to be in
///     _dirs unless explicitly seeded. Test accounts for this.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class NfsStorageContractTests : IStorageContractTests
{
    private FaultyLibNfs _fake = new();
    private NfsStorageDriver? _driver;
    private RemoteStorage? _storage;

    protected override IStorage CreateStorage()
    {
        _fake = new();

        // Seed root "/" so DirectoryExists("") → ToNfsPath("") = "/" → stat "/" → exists
        _fake.SeedDir(path: "/");

        NfsDriverConfig config = NfsDriverConfig.For(server: "fake-server", export: "/export");
        _driver = new(config: config, libNfs: _fake, log: NullLogger.Instance);
        _storage = new(driver: _driver);
        return _storage;
    }

    protected override Task SeedFile(string relativePath, byte[] content)
    {
        // FaultyLibNfs.Seed takes any path and normalises internally.
        // We pass relative path; Normalise() will prepend "/".
        _fake.Seed(path: relativePath, content: content);
        return Task.CompletedTask;
    }

    protected override Task SeedDirectory(string relativePath)
    {
        _fake.SeedDir(path: relativePath);
        return Task.CompletedTask;
    }

    protected override Task<bool> BackendHasFile(string relativePath)
    {
        // FaultyLibNfs.Files keys use the Normalise() form: leading slash, no trailing slash.
        string key = relativePath.Replace(oldChar: '\\', newChar: '/');
        if (!key.StartsWith(value: '/'))
            key = "/" + key;
        bool exists = _fake.Files.ContainsKey(key: key);
        return Task.FromResult(result: exists);
    }

    protected override Task DisposeStorage()
    {
        _driver?.Dispose();
        _driver = null;
        _storage = null;
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // NFSC-1, NFSC-2, NFSC-3 (originally documented gaps) — all closed:
    //   * FaultyLibNfs.ReadDir now walks the in-memory _files+_dirs map.
    //   * FaultyLibNfs.Stat64 returns a recorded mtime per path.
    //   * RemoteStorage.V() runs StoragePathGuard.StructuralValidate which
    //     rejects null bytes, ".." traversal, and absolute paths uniformly.
    // The corresponding base-class assertions now apply directly — no
    // overrides needed.
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // NFSC-4: ExistsAsync("") root check — NFS-specific behavior documented
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public override async Task Exists_empty_string_root_returns_true_when_root_is_directory()
    {
        // NFSC-4: RemoteStorage.ExistsAsync("") passes "" to NfsStorageDriver.FileExists("")
        // and DirectoryExists(""). ToNfsPath("") produces "/". FaultyLibNfs.Stat64("/")
        // checks _dirs["/"], which is seeded in CreateStorage() so this should return true.
        await base.Exists_empty_string_root_returns_true_when_root_is_directory();
    }
}
