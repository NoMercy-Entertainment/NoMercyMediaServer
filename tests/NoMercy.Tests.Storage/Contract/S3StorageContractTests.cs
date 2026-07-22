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

using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using NoMercy.Storage.Remote;
using NoMercy.Tests.Storage.Container;

namespace NoMercy.Tests.Storage.Contract;

/// <summary>
/// Runs the shared IStorage contract suite against the shared MinIO (S3) backend
/// in the all-in-one StorageBackends container.
///
/// Design:
///   - <see cref="StorageBackendsFixture"/> starts one container for the whole assembly.
///   - Each test in the base class calls <see cref="CreateStorage"/> then
///     <see cref="DisposeStorage"/> in its own try/finally.
///   - <see cref="CreateStorage"/> calls <c>Skip.If(!fixture.Available)</c> so every
///     inherited test skips cleanly when Docker is absent — the base class [Fact]
///     attributes remain, meaning the skip shows as "Skipped" rather than "Not Run".
///   - Seed and backend-check helpers use the raw AWS SDK client directly so they
///     don't exercise the abstraction under test.
/// </summary>
[Collection(name: "StorageBackends")]
[Trait(name: "Category", value: "Integration")]
public sealed class S3StorageContractTests(StorageBackendsFixture fixture) : IStorageContractTests
{
    // -----------------------------------------------------------------------
    // IStorageContractTests hooks
    // -----------------------------------------------------------------------

    protected override IStorage CreateStorage()
    {
        Skip.If(condition: !fixture.Available, reason: fixture.StartupError ?? "storage container not available");

        return new RemoteStorage(driver: fixture.BuildS3Driver());
    }

    /// <summary>
    /// Seed bypasses the driver under test — PUT directly via the raw SDK client.
    /// </summary>
    protected override async Task SeedFile(string relativePath, byte[] content)
    {
        using AmazonS3Client client = fixture.BuildS3RawClient();

        PutObjectRequest request = new()
        {
            BucketName = StorageBackendsFixture.S3Bucket,
            Key = relativePath.TrimStart(trimChar: '/'),
            InputStream = new MemoryStream(buffer: content),
            ContentType = "application/octet-stream",
        };

        await client.PutObjectAsync(request: request);
    }

    /// <summary>
    /// S3 has no real directory objects. Seed a zero-byte "path/" placeholder so
    /// ListAsync sees the prefix and ExistsAsync works for the directory entry.
    /// </summary>
    protected override async Task SeedDirectory(string relativePath)
    {
        using AmazonS3Client client = fixture.BuildS3RawClient();

        string key = relativePath.TrimStart(trimChar: '/').TrimEnd(trimChar: '/') + "/";

        PutObjectRequest request = new()
        {
            BucketName = StorageBackendsFixture.S3Bucket,
            Key = key,
            InputStream = new MemoryStream(buffer: Array.Empty<byte>()),
            ContentType = "application/x-directory",
        };

        await client.PutObjectAsync(request: request);
    }

    /// <summary>
    /// Verify file existence directly via HeadObject, bypassing the driver.
    /// </summary>
    protected override async Task<bool> BackendHasFile(string relativePath)
    {
        using AmazonS3Client client = fixture.BuildS3RawClient();

        try
        {
            GetObjectMetadataRequest request = new()
            {
                BucketName = StorageBackendsFixture.S3Bucket,
                Key = relativePath.TrimStart(trimChar: '/'),
            };

            await client.GetObjectMetadataAsync(request: request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    protected override Task DisposeStorage()
    {
        // RemoteStorage wraps S3StorageDriver which is IDisposable — dispose it.
        // Nothing else to clean; the container persists across tests.
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------------
    // S3-specific overrides — document known driver divergences
    // -----------------------------------------------------------------------

    // Absolute path rejection: RemoteStorage.V() calls StructuralValidate
    // which throws StoragePathNotAllowedException for leading-slash, drive-letter,
    // and UNC paths before the S3 driver is reached.

    // Null-byte rejection: StructuralValidate throws StoragePathNotAllowedException
    // before the SDK is called, satisfying the base contract's "throws" assertion.

    // double-slash normalisation: S3 preserves the double-slash in the key.
    // "foo//bar.bin" != "foo/bar.bin" in S3 key space.
    // The base contract asserts withDouble == withSingle; this WILL FAIL for S3.
    // Documented here as a known failure (separate named test so xUnit1024 is satisfied).
    [SkippableFact]
    [Trait(name: "Category", value: "Integration")]
    public async Task S3_double_slash_is_known_failure_requires_driver_normalisation()
    {
        Skip.If(condition: !fixture.Available, reason: fixture.StartupError ?? "storage container not available");

        IStorage storage = CreateStorage();
        try
        {
            byte[] data = new byte[] { 0x01, 0x02 };
            await SeedFile(relativePath: "foo/bar.bin", content: data);

            bool withSingle = await storage.ExistsAsync(path: "foo/bar.bin", ct: CancellationToken.None);

            // KNOWN FAILURE: S3StorageDriver does not collapse double slashes before
            // building S3 keys, so "foo//bar.bin" is a distinct key from "foo/bar.bin".
            // This assertion is expected to fail until the driver normalises input paths.
            bool withDouble = await storage.ExistsAsync(path: "foo//bar.bin", ct: CancellationToken.None);

            withSingle.Should().BeTrue();
            withDouble
                .Should()
                .Be(
                    expected: withSingle,
                    because: "KNOWN FAILURE: S3StorageDriver does not collapse double slashes — driver fix needed"
                );
        }
        finally
        {
            await DisposeStorage();
        }
    }

    // List_flat_does_not_see_subdir_contents: S3 LIST with delimiter "/" works
    // correctly. Expected to pass.

    // List_recursive_sees_subdir_contents: S3 LIST without delimiter returns
    // all keys. Expected to pass.
}
