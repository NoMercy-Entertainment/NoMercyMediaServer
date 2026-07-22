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

using NoMercy.Storage.Drivers.Local;
using NoMercy.Storage.Validation;

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Contract test driver for <see cref="LocalStorage"/>.
/// Uses a real temp directory as the backend; the <see cref="StoragePathGuard"/>
/// is configured with that directory as its single allowed root so all path
/// enforcement is live.
///
/// Seed* methods write directly via System.IO so no IStorage abstraction is
/// involved in setup — failures here are driver fidelity failures, not setup bugs.
/// BackendHasFile reads via System.IO for the same reason.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public sealed class LocalStorageContractTests : IStorageContractTests
{
    private string _root = string.Empty;
    private LocalStorage? _storage;

    protected override IStorage CreateStorage()
    {
        _root = Path.Combine(path1: Path.GetTempPath(), path2: $"nm-contract-local-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path: _root);

        LocalStorageDriver driver = new();
        StoragePathGuard guard = new(allowedRoots: [_root], driver: driver);
        _storage = new(driver: driver, guard: guard);
        return _storage;
    }

    protected override Task SeedFile(string relativePath, byte[] content)
    {
        string full = ToFull(relativePath: relativePath);
        string? dir = Path.GetDirectoryName(path: full);
        if (!string.IsNullOrEmpty(value: dir))
            Directory.CreateDirectory(path: dir);
        File.WriteAllBytes(path: full, bytes: content);
        return Task.CompletedTask;
    }

    protected override Task SeedDirectory(string relativePath)
    {
        Directory.CreateDirectory(path: ToFull(relativePath: relativePath));
        return Task.CompletedTask;
    }

    protected override Task<bool> BackendHasFile(string relativePath)
    {
        bool exists = File.Exists(path: ToFull(relativePath: relativePath));
        return Task.FromResult(result: exists);
    }

    protected override Task DisposeStorage()
    {
        try
        {
            if (!string.IsNullOrEmpty(value: _root) && Directory.Exists(path: _root))
                Directory.Delete(path: _root, recursive: true);
        }
        catch
        {
            // best-effort
        }
        _storage = null;
        return Task.CompletedTask;
    }

    private string ToFull(string relativePath)
    {
        string normalized = relativePath.Replace(oldChar: '\\', newChar: '/').TrimStart(trimChar: '/');
        return Path.Combine(path1: _root, path2: normalized.Replace(oldChar: '/', newChar: Path.DirectorySeparatorChar));
    }

    // -----------------------------------------------------------------------
    // LocalStorage-specific: absolute path MUST throw StoragePathNotAllowedException
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public override async Task Exists_absolute_path_is_rejected_or_returns_false()
    {
        IStorage storage = CreateStorage();
        try
        {
            string absolutePath = OperatingSystem.IsWindows()
                ? @"C:\Windows\System32\drivers\etc\hosts"
                : "/etc/hosts";

            Func<Task> act = () => storage.ExistsAsync(path: absolutePath, ct: CancellationToken.None);
            await act.Should()
                .ThrowAsync<StoragePathNotAllowedException>(
                    because: "LocalStorage with a configured root must reject any path outside that root"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // LocalStorage-specific: ".." traversal MUST throw StoragePathNotAllowedException
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public override async Task Exists_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Func<Task> act = () => storage.ExistsAsync(path: "../escape", ct: CancellationToken.None);
            await act.Should()
                .ThrowAsync<StoragePathNotAllowedException>(
                    because: "'..' escaping the allowed root must be rejected with StoragePathNotAllowedException"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // Rule 6 contract: StorageEntry.Path from List* is scope-relative
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task List_entries_are_scope_relative_and_round_trip_into_Exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            await SeedFile(relativePath: "movies/avatar/avatar.mkv", content: [0x01, 0x02]);
            await SeedDirectory(relativePath: "movies/avatar");

            List<StorageEntry> entries = [];
            await foreach (
                StorageEntry e in storage.ListAsync(
                    path: "movies/avatar",
                    pattern: "*",
                    recursive: true,
                    ct: CancellationToken.None
                )
            )
                entries.Add(item: e);

            entries.Should().NotBeEmpty();

            foreach (StorageEntry entry in entries)
            {
                entry
                    .Path.Should()
                    .NotContain(unexpected: ":\\", because: "StorageEntry.Path must not contain a Windows drive letter");
                entry
                    .Path.Should()
                    .NotStartWith(unexpected: "/", because: "StorageEntry.Path must not start with a leading slash");
                entry
                    .Path.ToLowerInvariant()
                    .Should()
                    .NotContain(
                        unexpected: _root.ToLowerInvariant(),
                        because: "StorageEntry.Path must not contain the OS root prefix"
                    );

                bool roundTrip = await storage.ExistsAsync(path: entry.Path, ct: CancellationToken.None);
                roundTrip
                    .Should()
                    .BeTrue(
                        because: $"StorageEntry.Path '{entry.Path}' returned from List must be passable back into Exists"
                    );
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public async Task List_sync_entries_are_scope_relative_and_round_trip_into_Exists()
    {
        IStorage storage = CreateStorage();
        try
        {
            await SeedFile(relativePath: "shows/breaking-bad/s01e01.mkv", content: [0x03, 0x04]);

            IReadOnlyList<StorageEntry> entries = storage.List(
                path: "shows/breaking-bad",
                pattern: "*",
                recursive: false
            );

            entries.Should().NotBeEmpty();

            foreach (StorageEntry entry in entries)
            {
                entry
                    .Path.Should()
                    .NotContain(unexpected: ":\\", because: "StorageEntry.Path must not contain a Windows drive letter");
                entry
                    .Path.Should()
                    .NotStartWith(unexpected: "/", because: "StorageEntry.Path must not start with a leading slash");
                entry
                    .Path.ToLowerInvariant()
                    .Should()
                    .NotContain(
                        unexpected: _root.ToLowerInvariant(),
                        because: "StorageEntry.Path must not contain the OS root prefix"
                    );

                bool roundTrip = await storage.ExistsAsync(path: entry.Path, ct: CancellationToken.None);
                roundTrip
                    .Should()
                    .BeTrue(
                        because: $"StorageEntry.Path '{entry.Path}' returned from List (sync) must be passable back into Exists"
                    );
            }
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // -----------------------------------------------------------------------
    // GetFullPath contract
    // -----------------------------------------------------------------------

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public void GetFullPath_scope_relative_returns_os_absolute_under_root()
    {
        IStorage storage = CreateStorage();
        try
        {
            string result = storage.GetFullPath(path: "movies/avatar/avatar.mkv");

            Path.IsPathRooted(path: result)
                .Should()
                .BeTrue(because: "GetFullPath must return an OS-absolute path");
            result
                .ToLowerInvariant()
                .Should()
                .StartWith(expected: _root.ToLowerInvariant(), because: "result must be under the configured root");
        }
        finally
        {
            _storage = null;
            if (!string.IsNullOrEmpty(value: _root) && Directory.Exists(path: _root))
                Directory.Delete(path: _root, recursive: true);
        }
    }

    [SkippableFact]
    [Trait(name: "Category", value: "Unit")]
    public void GetFullPath_dotdot_traversal_throws()
    {
        IStorage storage = CreateStorage();
        try
        {
            Action act = () => storage.GetFullPath(path: "../escape/secret.txt");
            act.Should()
                .Throw<StoragePathNotAllowedException>(
                    because: ".. traversal must be rejected by GetFullPath"
                );
        }
        finally
        {
            _storage = null;
            if (!string.IsNullOrEmpty(value: _root) && Directory.Exists(path: _root))
                Directory.Delete(path: _root, recursive: true);
        }
    }
}
