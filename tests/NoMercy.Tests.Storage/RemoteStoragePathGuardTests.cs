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
using NoMercy.Storage.Drivers.S3;
using NoMercy.Storage.Drivers.WebDav;
using NoMercy.Storage.Remote;
using NoMercy.Storage.Validation;
using NoMercy.Tests.Storage.Faults;
using WebDav;

namespace NoMercy.Tests.Storage;

/// <summary>
/// Focused unit tests for the absolute-path-rejection gap closed in
/// RemoteStorage. Exercises all four absolute-path forms against each
/// remote driver shape (NFS/S3/WebDAV) via the IStorage entry point.
///
/// The four rejected forms are:
///   - Unix-rooted leading slash    /mnt/media/x.mkv
///   - Windows drive prefix         C:\Media\x.mkv  or  C:/Media/x.mkv
///   - UNC share path               \\server\share\file
///   - Windows backslash root       \relative\but\rooted
///
/// Empty string must still pass (Rule 3 — empty = scope root).
/// </summary>
[Trait("Category", "Unit")]
public sealed class RemoteStoragePathGuardTests
{
    // -----------------------------------------------------------------------
    // Helpers — build a minimal RemoteStorage for each driver type
    // -----------------------------------------------------------------------

    private static RemoteStorage BuildNfsStorage()
    {
        FaultyLibNfs fake = new();
        fake.SeedDir("/");
        NfsDriverConfig config = NfsDriverConfig.For("fake-server", "/export");
        NfsStorageDriver driver = new(config, fake, NullLogger.Instance);
        return new RemoteStorage(driver);
    }

    private static RemoteStorage BuildS3Storage()
    {
        S3StorageDriver driver = new(
            bucket: "test-bucket",
            region: "us-east-1",
            prefix: null,
            endpoint: "http://localhost:19999",
            accessKey: "test-access-key",
            secretKey: "test-secret-key"
        );
        return new RemoteStorage(driver);
    }

    private static RemoteStorage BuildWebDavStorage()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri("http://localhost:19998/") };
        WebDavClient client = new(httpClient);
        WebDavStorageDriver driver = new(client, "http://localhost:19998/");
        return new RemoteStorage(driver);
    }

    // -----------------------------------------------------------------------
    // Theory data — all absolute-path forms that must be rejected
    // -----------------------------------------------------------------------

    public static TheoryData<string, string> AbsolutePathForms =>
        new()
        {
            { "/mnt/media/x.mkv", "Unix-rooted leading slash" },
            { "/abs/path/escape", "Unix-rooted single slash" },
            { @"C:\Media\x.mkv", "Windows drive + backslash" },
            { "C:/Media/x.mkv", "Windows drive + forward slash" },
            { @"\\server\share\file", "UNC path double backslash" },
            { @"\relative-rooted", "Windows backslash root" },
        };

    // -----------------------------------------------------------------------
    // NFS
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task NFS_ExistsAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildNfsStorage();
        Func<Task> act = () => storage.ExistsAsync(path, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"NFS RemoteStorage must reject absolute path form: {form}"
            );
    }

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task NFS_ReadAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildNfsStorage();
        Func<Task> act = () => storage.ReadAsync(path, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"NFS RemoteStorage must reject absolute path form: {form}"
            );
    }

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task NFS_WriteAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildNfsStorage();
        Func<Task> act = () =>
            storage.WriteAsync(path, new byte[] { 0x01 }, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"NFS RemoteStorage must reject absolute path form: {form}"
            );
    }

    // -----------------------------------------------------------------------
    // S3
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task S3_ExistsAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildS3Storage();
        Func<Task> act = () => storage.ExistsAsync(path, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"S3 RemoteStorage must reject absolute path form: {form}"
            );
    }

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task S3_WriteAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildS3Storage();
        Func<Task> act = () =>
            storage.WriteAsync(path, new byte[] { 0x01 }, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"S3 RemoteStorage must reject absolute path form: {form}"
            );
    }

    // -----------------------------------------------------------------------
    // WebDAV
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task WebDav_ExistsAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildWebDavStorage();
        Func<Task> act = () => storage.ExistsAsync(path, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"WebDAV RemoteStorage must reject absolute path form: {form}"
            );
    }

    [Theory]
    [MemberData(nameof(AbsolutePathForms))]
    public async Task WebDav_WriteAsync_rejects_absolute_path(string path, string form)
    {
        RemoteStorage storage = BuildWebDavStorage();
        Func<Task> act = () =>
            storage.WriteAsync(path, new byte[] { 0x01 }, CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                $"WebDAV RemoteStorage must reject absolute path form: {form}"
            );
    }

    // -----------------------------------------------------------------------
    // Rule 3 — empty string must still pass for all driver shapes
    // -----------------------------------------------------------------------

    [Fact]
    public void NFS_V_empty_string_does_not_throw()
    {
        RemoteStorage storage = BuildNfsStorage();
        // ExistsAsync("") delegates to NFS; the FaultyLibNfs has "/" seeded.
        // We just need V() to not throw — the downstream result doesn't matter here.
        Func<Task> act = async () =>
        {
            try
            {
                await storage.ExistsAsync("", CancellationToken.None);
            }
            catch (StoragePathNotAllowedException ex)
            {
                throw new InvalidOperationException(
                    $"Rule 3 violated: empty string must not be rejected by V(), got: {ex.Message}"
                );
            }
            catch
            {
                // Any other driver error from the fake is acceptable.
            }
        };

        act.Should().NotThrowAsync("empty string must pass structural validation (Rule 3)");
    }

    [Fact]
    public void S3_V_empty_string_does_not_throw()
    {
        RemoteStorage storage = BuildS3Storage();
        Func<Task> act = async () =>
        {
            try
            {
                await storage.ExistsAsync("", CancellationToken.None);
            }
            catch (StoragePathNotAllowedException ex)
            {
                throw new InvalidOperationException(
                    $"Rule 3 violated: empty string must not be rejected by V(), got: {ex.Message}"
                );
            }
            catch
            {
                // Network error from non-existent MinIO is acceptable.
            }
        };

        act.Should().NotThrowAsync("empty string must pass structural validation (Rule 3)");
    }

    [Fact]
    public void WebDav_V_empty_string_does_not_throw()
    {
        RemoteStorage storage = BuildWebDavStorage();
        Func<Task> act = async () =>
        {
            try
            {
                await storage.ExistsAsync("", CancellationToken.None);
            }
            catch (StoragePathNotAllowedException ex)
            {
                throw new InvalidOperationException(
                    $"Rule 3 violated: empty string must not be rejected by V(), got: {ex.Message}"
                );
            }
            catch
            {
                // Network error from non-existent WebDAV is acceptable.
            }
        };

        act.Should().NotThrowAsync("empty string must pass structural validation (Rule 3)");
    }

    // -----------------------------------------------------------------------
    // Contract extension — base IStorageContractTests absolute-path test
    // now expects a throw (not just "returned false") for NFS
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NFS_contract_absolute_path_throws_not_returns_false()
    {
        RemoteStorage storage = BuildNfsStorage();
        Func<Task> act = () => storage.ExistsAsync("/abs/path/escape", CancellationToken.None);
        await act.Should()
            .ThrowAsync<StoragePathNotAllowedException>(
                "absolute path must now throw, not silently return false, for NFS via RemoteStorage"
            );
    }
}
