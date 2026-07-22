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

using NoMercy.Storage.Drivers.Smb;
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Runs the shared IStorage contract suite against the Samba backend in the
/// all-in-one StorageBackends container.
///
/// Seeding and verification bypass the instance under test by using a separate
/// short-lived <see cref="SmbStorageDriver"/> — the driver is the SMB protocol
/// client, and a distinct instance shares no session state with the one being
/// exercised, so it is a valid out-of-band seed/verify path.
/// </summary>
[Collection(name: "StorageBackends")]
[Trait(name: "Category", value: "Integration")]
public sealed class SmbStorageContractTests(StorageBackendsFixture fixture) : IStorageContractTests
{
    protected override IStorage CreateStorage()
    {
        Skip.If(condition: !fixture.Available, reason: fixture.StartupError ?? "storage container not available");
        return new RemoteStorage(driver: fixture.BuildSmbDriver());
    }

    protected override async Task SeedFile(string relativePath, byte[] content)
    {
        string normalized = relativePath.Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/');
        string? parent = Path.GetDirectoryName(path: normalized)?.Replace(oldChar: '\\', newChar: '/');
        using SmbStorageDriver seed = fixture.BuildSmbDriver();
        if (!string.IsNullOrEmpty(value: parent))
            seed.CreateDirectory(path: parent);

        await using Stream w = seed.OpenWrite(path: normalized, overwrite: true);
        await w.WriteAsync(buffer: content);
    }

    protected override Task SeedDirectory(string relativePath)
    {
        string normalized = relativePath.Replace(oldChar: '\\', newChar: '/').Trim(trimChar: '/');
        using SmbStorageDriver seed = fixture.BuildSmbDriver();
        seed.CreateDirectory(path: normalized);
        return Task.CompletedTask;
    }

    protected override Task<bool> BackendHasFile(string relativePath)
    {
        string normalized = relativePath.Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/');
        using SmbStorageDriver verify = fixture.BuildSmbDriver();
        return Task.FromResult(result: verify.FileExists(path: normalized));
    }

    protected override Task DisposeStorage()
    {
        // Per-test isolation: wipe the share root so each test starts clean.
        if (!fixture.Available)
            return Task.CompletedTask;

        try
        {
            using SmbStorageDriver cleaner = fixture.BuildSmbDriver();
            foreach (
                string entry in cleaner.EnumerateFileSystemEntries(
                    directory: string.Empty,
                    searchPattern: "*",
                    option: SearchOption.TopDirectoryOnly
                )
            )
            {
                try
                {
                    if (cleaner.DirectoryExists(path: entry))
                        cleaner.DeleteDirectory(path: entry, recursive: true);
                    else
                        cleaner.DeleteFile(path: entry);
                }
                catch
                {
                    // Best effort — one stuck entry shouldn't fail the next test.
                }
            }
        }
        catch
        {
            // Best effort — cleanup failures shouldn't fail the test.
        }

        return Task.CompletedTask;
    }
}
